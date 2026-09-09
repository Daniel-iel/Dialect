namespace Dialect.Core.QueryTranslation;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dialect.Core.AST;

internal static class SimpleDmlAstParser
{
    private static readonly Regex IdentifierRegex = new(@"^\s*(\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.Compiled);

    public static QueryNode? Parse(string sql, SqlProvider sourceProvider)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        var s = sql.Trim().TrimEnd(';').Trim();
        if (s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return SimpleSelectAstParser.Parse(s, sourceProvider);
        if (s.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            return ParseInsert(s);
        if (s.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            return ParseUpdate(s);
        if (s.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            return ParseDelete(s);

        return null;
    }

    private static InsertStatement? ParseInsert(string sql)
    {
        var m = Regex.Match(
            sql,
            @"^INSERT\s+INTO\s+(?<table>\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)\s*\((?<cols>[^)]*)\)\s*VALUES\s*(?<values>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return null;

        if (!TryParseTable(m.Groups["table"].Value, out var table))
            return null;

        var columns = ParseColumnList(m.Groups["cols"].Value);
        if (columns.Count == 0)
            return null;

        var rows = ParseValuesRows(m.Groups["values"].Value);
        if (rows == null || rows.Count == 0)
            return null;

        return new InsertStatement(table, columns, rows);
    }

    private static UpdateStatement? ParseUpdate(string sql)
    {
        var m = Regex.Match(
            sql,
            @"^UPDATE\s+(?<table>\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)\s+SET\s+(?<set>.+?)(?:\s+WHERE\s+(?<where>.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return null;

        if (!TryParseTable(m.Groups["table"].Value, out var table))
            return null;

        var assignments = ParseAssignments(m.Groups["set"].Value);
        if (assignments == null || assignments.Count == 0)
            return null;

        var whereText = m.Groups["where"].Success ? m.Groups["where"].Value.Trim() : null;
        var where = ParseWhere(whereText);
        if (whereText != null && where is null)
            return null;

        return new UpdateStatement(table, assignments, where, AllowFullTableUpdate: true);
    }

    private static DeleteStatement? ParseDelete(string sql)
    {
        var m = Regex.Match(
            sql,
            @"^DELETE\s+FROM\s+(?<table>\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)\s*(?:WHERE\s+(?<where>.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return null;

        if (!TryParseTable(m.Groups["table"].Value, out var table))
            return null;

        var whereText = m.Groups["where"].Success ? m.Groups["where"].Value.Trim() : null;
        var where = ParseWhere(whereText);
        if (whereText != null && where is null)
            return null;

        return new DeleteStatement(table, where, AllowFullTableDelete: true);
    }

    private static IReadOnlyList<IReadOnlyList<object?>>? ParseValuesRows(string valuesText)
    {
        var rows = new List<IReadOnlyList<object?>>();
        var items = SplitTopLevel(valuesText.Trim(), ',');

        var mergedRows = MergeParenthesizedRows(items);
        foreach (var rowText in mergedRows)
        {
            var trimmed = rowText.Trim();
            if (!trimmed.StartsWith("(") || !trimmed.EndsWith(")"))
                return null;

            var inner = trimmed[1..^1];
            var values = new List<object?>();
            foreach (var rawValue in SplitTopLevel(inner, ','))
            {
                if (!TryParseLiteral(rawValue.Trim(), out var value))
                    return null;
                values.Add(value);
            }

            rows.Add(values);
        }

        return rows;
    }

    private static List<string> MergeParenthesizedRows(List<string> chunks)
    {
        var rows = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inString = false;

        foreach (var chunk in chunks)
        {
            var text = chunk.Trim();
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(text);

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\'')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        i++;
                        continue;
                    }
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (ch == '(')
                    depth++;
                else if (ch == ')')
                    depth--;
            }

            if (depth == 0)
            {
                rows.Add(sb.ToString());
                sb.Clear();
            }
        }

        return rows;
    }

    private static IReadOnlyDictionary<Column, object?>? ParseAssignments(string setText)
    {
        var result = new Dictionary<Column, object?>();

        foreach (var part in SplitTopLevel(setText, ','))
        {
            var assignment = part.Trim();
            var eqIndex = assignment.IndexOf('=');
            if (eqIndex <= 0)
                return null;

            var left = assignment[..eqIndex].Trim();
            var right = assignment[(eqIndex + 1)..].Trim();

            if (!TryParseIdentifier(left, out var columnName))
                return null;
            if (!TryParseLiteral(right, out var value))
                return null;

            result[new Column(columnName)] = value;
        }

        return result;
    }

    private static WhereExpression? ParseWhere(string? whereText)
    {
        if (string.IsNullOrWhiteSpace(whereText))
            return null;

        var expression = whereText.Trim().TrimEnd(';').Trim();
        var orParts = SplitByLogicalOperator(expression, "OR");
        if (orParts.Count > 1)
        {
            WhereExpression? aggregate = null;
            foreach (var part in orParts)
            {
                var parsed = ParseWhereAnd(part);
                if (parsed is null)
                    return null;
                aggregate = aggregate is null ? parsed : new OrNode(aggregate, parsed);
            }

            return aggregate;
        }

        return ParseWhereAnd(expression);
    }

    private static WhereExpression? ParseWhereAnd(string expression)
    {
        var andParts = SplitByLogicalOperator(expression, "AND");
        if (andParts.Count > 1)
        {
            WhereExpression? aggregate = null;
            foreach (var part in andParts)
            {
                var parsed = ParseWhereAtom(part);
                if (parsed is null)
                    return null;
                aggregate = aggregate is null ? parsed : new AndNode(aggregate, parsed);
            }

            return aggregate;
        }

        return ParseWhereAtom(expression);
    }

    private static WhereExpression? ParseWhereAtom(string atom)
    {
        var s = atom.Trim();

        var mIsNull = Regex.Match(s, @"^(?<col>.+?)\s+IS\s+(?<not>NOT\s+)?NULL$", RegexOptions.IgnoreCase);
        if (mIsNull.Success)
        {
            if (!TryParseIdentifier(mIsNull.Groups["col"].Value, out var columnName))
                return null;

            return new ComparisonNode(
                new Column(columnName),
                mIsNull.Groups["not"].Success ? ComparisonOperator.IsNotNull : ComparisonOperator.IsNull,
                null);
        }

        var mIn = Regex.Match(s, @"^(?<col>.+?)\s+(?<not>NOT\s+)?IN\s*\((?<vals>.+)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (mIn.Success)
        {
            if (!TryParseIdentifier(mIn.Groups["col"].Value, out var inCol))
                return null;

            var vals = new List<object?>();
            foreach (var part in SplitTopLevel(mIn.Groups["vals"].Value, ','))
            {
                if (!TryParseLiteral(part.Trim(), out var value))
                    return null;
                vals.Add(value);
            }

            return new InNode(new Column(inCol), vals, Negated: mIn.Groups["not"].Success);
        }

        var mComp = Regex.Match(s, @"^(?<col>.+?)\s*(?<op><>|!=|<=|>=|=|<|>|LIKE|NOT\s+LIKE)\s*(?<val>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!mComp.Success)
            return null;

        if (!TryParseIdentifier(mComp.Groups["col"].Value, out var compCol))
            return null;
        if (!TryParseLiteral(mComp.Groups["val"].Value.Trim(), out var compValue))
            return null;

        var op = NormalizeOperator(mComp.Groups["op"].Value);
        return new ComparisonNode(new Column(compCol), op, compValue);
    }

    private static ComparisonOperator NormalizeOperator(string raw)
    {
        return raw.Trim().ToUpperInvariant() switch
        {
            "=" => ComparisonOperator.Equal,
            "<>" => ComparisonOperator.NotEqual,
            "!=" => ComparisonOperator.NotEqual,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessThanOrEqual,
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "LIKE" => ComparisonOperator.Like,
            "NOT LIKE" => ComparisonOperator.NotLike,
            _ => ComparisonOperator.Equal
        };
    }

    private static List<string> SplitByLogicalOperator(string text, string op)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '\'')
            {
                if (i + 1 < text.Length && text[i + 1] == '\'')
                {
                    sb.Append(ch).Append(text[i + 1]);
                    i++;
                    continue;
                }

                inString = !inString;
                sb.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                    depth++;
                else if (ch == ')' && depth > 0)
                    depth--;
            }

            if (!inString && depth == 0 && IsOperatorAt(text, i, op))
            {
                parts.Add(sb.ToString().Trim());
                sb.Clear();
                i += op.Length - 1;
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
            parts.Add(sb.ToString().Trim());

        return parts.Where(p => p.Length > 0).ToList();
    }

    private static bool IsOperatorAt(string text, int index, string op)
    {
        if (index + op.Length > text.Length)
            return false;

        var chunk = text.Substring(index, op.Length);
        if (!chunk.Equals(op, StringComparison.OrdinalIgnoreCase))
            return false;

        var leftBoundary = index == 0 || char.IsWhiteSpace(text[index - 1]) || text[index - 1] == '(';
        var rightBoundary = index + op.Length == text.Length || char.IsWhiteSpace(text[index + op.Length]) || text[index + op.Length] == ')';

        return leftBoundary && rightBoundary;
    }

    private static List<Column> ParseColumnList(string text)
    {
        var columns = new List<Column>();
        foreach (var item in SplitTopLevel(text, ','))
        {
            if (!TryParseIdentifier(item.Trim(), out var name))
                return [];
            columns.Add(new Column(name));
        }

        return columns;
    }

    private static bool TryParseTable(string token, out TableReference table)
    {
        if (!TryParseIdentifier(token, out var name))
        {
            table = new TableReference(string.Empty);
            return false;
        }

        table = new TableReference(name);
        return true;
    }

    private static bool TryParseIdentifier(string token, out string identifier)
    {
        var m = IdentifierRegex.Match(token);
        if (!m.Success)
        {
            identifier = string.Empty;
            return false;
        }

        identifier = UnquoteIdentifier(m.Groups[1].Value);
        return !string.IsNullOrWhiteSpace(identifier);
    }

    private static string UnquoteIdentifier(string value)
    {
        var s = value.Trim();
        if (s.StartsWith("[") && s.EndsWith("]"))
            return s[1..^1];
        if (s.StartsWith("`") && s.EndsWith("`"))
            return s[1..^1];
        if (s.StartsWith("\"") && s.EndsWith("\""))
            return s[1..^1];
        return s;
    }

    private static bool TryParseLiteral(string token, out object? value)
    {
        var s = token.Trim();
        if (string.Equals(s, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            return true;
        }

        if (s.StartsWith("N'", StringComparison.OrdinalIgnoreCase))
            s = s[1..];

        if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2)
        {
            value = s[1..^1].Replace("''", "'");
            return true;
        }

        if (bool.TryParse(s, out var b))
        {
            value = b;
            return true;
        }

        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            value = l;
            return true;
        }

        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            value = d;
            return true;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            value = dt;
            return true;
        }

        value = null;
        return false;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '\'')
            {
                if (i + 1 < text.Length && text[i + 1] == '\'')
                {
                    sb.Append(ch).Append(text[i + 1]);
                    i++;
                    continue;
                }

                inString = !inString;
                sb.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                    depth++;
                else if (ch == ')' && depth > 0)
                    depth--;
            }

            if (!inString && depth == 0 && ch == separator)
            {
                parts.Add(sb.ToString().Trim());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
            parts.Add(sb.ToString().Trim());

        return parts;
    }
}
