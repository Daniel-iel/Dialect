namespace Dialect.Core.QueryTranslation;

using System.Text.RegularExpressions;
using Dialect.Core.AST;

internal static class SimpleSelectAstParser
{
    public static SelectStatement? Parse(string sql, SqlProvider sourceProvider)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        var s = sql.Trim().TrimEnd(';').Trim();
        if (!s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return null;

        if (ContainsSetOperators(s))
            return null;

        var fromIndex = IndexOfKeyword(s, "FROM");
        if (fromIndex < 0)
            return null;

        var selectPart = s["SELECT".Length..fromIndex].Trim();
        var tail = s[(fromIndex + "FROM".Length)..].Trim();

        var parsedSelect = ParseSelectPart(selectPart, sourceProvider);
        if (parsedSelect == null)
            return null;

        var tailSections = SplitTailSections(tail);
        if (tailSections == null)
            return null;

        if (tailSections.Value.Where is not null || tailSections.Value.GroupBy is not null || tailSections.Value.Having is not null)
            return null;

        var from = ParseFrom(tailSections.Value.From);
        if (from == null)
            return null;

        var columns = ParseColumns(parsedSelect.Value.ColumnsPart);
        if (columns.Count == 0)
            return null;

        var orderBy = ParseOrderBy(tailSections.Value.OrderBy);
        var rowLimit = ParseRowLimit(
            parsedSelect.Value.Top,
            parsedSelect.Value.TopWithTies,
            tailSections.Value.Limit,
            tailSections.Value.Offset,
            tailSections.Value.FetchNext,
            sourceProvider);

        return new SelectStatement(
            Columns: columns,
            From: from,
            OrderByClauses: orderBy.Count > 0 ? orderBy : null,
            RowLimit: rowLimit,
            IsDistinct: parsedSelect.Value.IsDistinct);
    }

    private static bool ContainsSetOperators(string sql)
    {
        return Regex.IsMatch(sql, @"\b(UNION|INTERSECT|EXCEPT)\b", RegexOptions.IgnoreCase);
    }

    private static (bool IsDistinct, int? Top, bool TopWithTies, string ColumnsPart)? ParseSelectPart(string selectPart, SqlProvider sourceProvider)
    {
        var current = selectPart.Trim();
        var isDistinct = false;

        if (current.StartsWith("DISTINCT ", StringComparison.OrdinalIgnoreCase))
        {
            isDistinct = true;
            current = current["DISTINCT ".Length..].Trim();
        }

        int? top = null;
        bool withTies = false;
        if (sourceProvider == SqlProvider.SqlServer)
        {
            var topMatch = Regex.Match(current, @"^TOP\s*\(?\s*(\d+)\s*\)?\s*(WITH\s+TIES)?\s+(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (topMatch.Success)
            {
                top = int.Parse(topMatch.Groups[1].Value);
                withTies = !string.IsNullOrWhiteSpace(topMatch.Groups[2].Value);
                current = topMatch.Groups[3].Value.Trim();
            }
        }

        return (isDistinct, top, withTies, current);
    }

    private static (string From, string? Where, string? GroupBy, string? Having, string? OrderBy, int? Limit, int? Offset, int? FetchNext)? SplitTailSections(string tail)
    {
        var clauses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in new[] { "WHERE", "GROUP BY", "HAVING", "ORDER BY", "LIMIT", "OFFSET", "FETCH NEXT" })
        {
            var idx = IndexOfKeyword(tail, keyword);
            if (idx >= 0)
                clauses[keyword] = idx;
        }

        var ordered = clauses.OrderBy(kv => kv.Value).ToList();
        var fromEnd = ordered.Count > 0 ? ordered[0].Value : tail.Length;
        var from = tail[..fromEnd].Trim();
        if (string.IsNullOrWhiteSpace(from))
            return null;

        string? Section(string name)
        {
            if (!clauses.TryGetValue(name, out var start))
                return null;

            var end = tail.Length;
            foreach (var kv in ordered)
            {
                if (kv.Value > start && kv.Value < end)
                    end = kv.Value;
            }

            var labelLength = name.Length;
            return tail[(start + labelLength)..end].Trim();
        }

        int? ParseLeadingInt(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = Regex.Match(text, @"^(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : null;
        }

        var limit = ParseLeadingInt(Section("LIMIT"));
        var offset = ParseLeadingInt(Section("OFFSET"));
        var fetchNext = ParseLeadingInt(Section("FETCH NEXT"));

        return (
            from,
            Section("WHERE"),
            Section("GROUP BY"),
            Section("HAVING"),
            Section("ORDER BY"),
            limit,
            offset,
            fetchNext);
    }

    private static TableReference? ParseFrom(string fromPart)
    {
        if (string.IsNullOrWhiteSpace(fromPart))
            return null;

        if (fromPart.Contains(",") || Regex.IsMatch(fromPart, @"\bJOIN\b", RegexOptions.IgnoreCase))
            return null;

        var tokens = fromPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return null;

        var tableName = UnquoteIdentifier(tokens[0]);
        string? alias = null;
        if (tokens.Length >= 3 && tokens[1].Equals("AS", StringComparison.OrdinalIgnoreCase))
            alias = UnquoteIdentifier(tokens[2]);
        else if (tokens.Length >= 2)
            alias = UnquoteIdentifier(tokens[1]);

        return new TableReference(tableName, alias);
    }

    private static List<Column> ParseColumns(string columnsPart)
    {
        var result = new List<Column>();
        foreach (var raw in SplitCommaSeparated(columnsPart))
        {
            var item = raw.Trim();
            if (string.IsNullOrWhiteSpace(item))
                continue;

            if (item == "*")
            {
                result.Add(new Column("*"));
                continue;
            }

            var asMatch = Regex.Match(item, @"^(.*?)(?:\s+AS\s+|\s+)([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var expression = item;
            string? alias = null;
            if (asMatch.Success)
            {
                expression = asMatch.Groups[1].Value.Trim();
                alias = asMatch.Groups[2].Value.Trim();
            }

            if (TryParseSimpleColumn(expression, out var column))
            {
                result.Add(column with { Alias = alias });
            }
            else
            {
                result.Add(new Column(expression, alias, null, IsRawExpression: true));
            }
        }

        return result;
    }

    private static List<OrderByClause> ParseOrderBy(string? orderByPart)
    {
        var result = new List<OrderByClause>();
        if (string.IsNullOrWhiteSpace(orderByPart))
            return result;

        foreach (var raw in SplitCommaSeparated(orderByPart))
        {
            var item = raw.Trim();
            if (string.IsNullOrWhiteSpace(item))
                continue;

            var desc = item.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase);
            var asc = item.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase);
            var name = (desc || asc) ? item[..item.LastIndexOf(' ')].Trim() : item;
            var direction = desc ? SortDirection.Descending : SortDirection.Ascending;

            if (TryParseSimpleColumn(name, out var column))
            {
                result.Add(new OrderByClause(column, direction));
            }
        }

        return result;
    }

    private static RowLimit? ParseRowLimit(int? top, bool withTies, int? limit, int? offset, int? fetchNext, SqlProvider sourceProvider)
    {
        if (top.HasValue)
            return new RowLimit(top.Value, null, withTies);

        if (sourceProvider == SqlProvider.SqlServer && (offset.HasValue || fetchNext.HasValue))
            return new RowLimit(fetchNext, offset);

        if (limit.HasValue || offset.HasValue)
            return new RowLimit(limit, offset);

        return null;
    }

    private static bool TryParseSimpleColumn(string value, out Column column)
    {
        var trimmed = value.Trim();
        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            column = new Column(UnquoteIdentifier(parts[0]));
            return true;
        }

        if (parts.Length == 2)
        {
            column = new Column(UnquoteIdentifier(parts[1]), null, UnquoteIdentifier(parts[0]));
            return true;
        }

        column = new Column(trimmed, null, null, IsRawExpression: true);
        return false;
    }

    private static List<string> SplitCommaSeparated(string text)
    {
        var parts = new List<string>();
        var sb = new System.Text.StringBuilder();
        var depth = 0;

        foreach (var c in text)
        {
            if (c == '(') depth++;
            if (c == ')' && depth > 0) depth--;

            if (c == ',' && depth == 0)
            {
                parts.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
            parts.Add(sb.ToString());

        return parts;
    }

    private static int IndexOfKeyword(string text, string keyword)
    {
        return Regex.Match(text, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase).Success
            ? Regex.Match(text, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase).Index
            : -1;
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
}
