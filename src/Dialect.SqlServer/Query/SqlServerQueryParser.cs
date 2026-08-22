namespace Dialect.SqlServer.Query;

using System.Text.RegularExpressions;
using Dialect.Core.Query;

/// <summary>
/// SQL Server-specific query parser.
/// Handles T-SQL syntax and optimizations.
/// </summary>
public class SqlServerQueryParser : QueryParser
{
    public override ParsedQuery Parse(string sql)
    {
        var cleanSql = sql.Trim();
        
        // Extract main clauses
        var selectClause = ExtractSelectClause(cleanSql);
        var selectColumns = ExtractColumns(selectClause);
        var fromClause = ExtractFromClause(cleanSql);
        var fromTables = ExtractTables(fromClause);
        var whereClauses = ExtractWhereClauses(cleanSql);
        var joinClauses = ExtractJoinClauses(cleanSql);
        var groupByClauses = ExtractGroupByClauses(cleanSql);
        var orderByClauses = ExtractOrderByClauses(cleanSql);
        
        // Estimate selectivity based on WHERE predicates
        var predicates = whereClauses.Select(c => c.RawSql).ToArray();
        var selectivity = EstimateSelectivity(predicates);
        
        // Calculate complexity
        int complexity = CountNestingDepth(cleanSql);
        
        var parsed = new ParsedQuery(
            SelectClause: selectClause,
            SelectColumns: selectColumns,
            FromClause: fromClause,
            FromTables: fromTables,
            WhereClauses: whereClauses,
            JoinClauses: joinClauses,
            GroupByClauses: groupByClauses,
            OrderByClauses: orderByClauses,
            SelectivityEstimate: selectivity,
            QueryComplexity: complexity
        );
        
        return parsed;
    }

    private static string ExtractSelectClause(string sql)
    {
        var match = Regex.Match(sql, @"SELECT\s+(.*?)\s+FROM", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string[] ExtractColumns(string clause)
    {
        if (string.IsNullOrEmpty(clause)) return Array.Empty<string>();
        
        return clause.Split(',')
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();
    }

    private static string ExtractFromClause(string sql)
    {
        var match = Regex.Match(sql, @"FROM\s+(.*?)(?:WHERE|JOIN|GROUP BY|ORDER BY|$)", 
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string[] ExtractTables(string fromClause)
    {
        if (string.IsNullOrEmpty(fromClause)) return Array.Empty<string>();
        
        return fromClause.Split(',', ' ')
            .Where(t => !string.IsNullOrEmpty(t) && !t.Contains("("))
            .Select(t => t.Trim())
            .ToArray();
    }

    private ParsedClause[] ExtractWhereClauses(string sql)
    {
        var match = Regex.Match(sql, @"WHERE\s+(.*?)(?:GROUP BY|ORDER BY|$)", RegexOptions.IgnoreCase);
        if (!match.Success) return Array.Empty<ParsedClause>();

        var whereText = match.Groups[1].Value;
        var predicates = whereText.Split(" AND ", StringSplitOptions.None).SelectMany(x => x.Split(" OR ", StringSplitOptions.None));
        
        return predicates
            .Select((pred, idx) => new ParsedClause(
                Type: "WHERE",
                RawSql: pred.Trim(),
                Columns: ExtractColumnsFromPredicate(pred),
                Tables: Array.Empty<string>(),
                Complexity: 1
            ))
            .ToArray();
    }

    private static ParsedClause[] ExtractJoinClauses(string sql)
    {
        var matches = Regex.Matches(sql, 
            @"(?:INNER|LEFT|RIGHT|FULL|CROSS)\s+(?:OUTER\s+)?JOIN\s+([^\s]+)\s+(?:ON|USING)\s+([^\s]+)", 
            RegexOptions.IgnoreCase);
        
        var joins = new List<ParsedClause>();
        foreach (Match match in matches)
        {
            joins.Add(new ParsedClause(
                Type: "JOIN",
                RawSql: match.Value,
                Columns: new[] { match.Groups[2].Value },
                Tables: new[] { match.Groups[1].Value },
                Complexity: 1
            ));
        }
        
        return joins.ToArray();
    }

    private static ParsedClause[] ExtractGroupByClauses(string sql)
    {
        var match = Regex.Match(sql, @"GROUP BY\s+(.*?)(?:HAVING|ORDER BY|$)", RegexOptions.IgnoreCase);
        if (!match.Success) return Array.Empty<ParsedClause>();

        var columns = match.Groups[1].Value.Split(',').Select(c => c.Trim()).ToArray();
        
        return new[]
        {
            new ParsedClause(
                Type: "GROUP_BY",
                RawSql: match.Groups[1].Value,
                Columns: columns,
                Tables: Array.Empty<string>(),
                Complexity: 1
            )
        };
    }

    private static ParsedClause[] ExtractOrderByClauses(string sql)
    {
        var match = Regex.Match(sql, @"ORDER BY\s+(.*?)$", RegexOptions.IgnoreCase);
        if (!match.Success) return Array.Empty<ParsedClause>();

        var orderText = match.Groups[1].Value;
        var columns = orderText.Split(',').Select(c => c.Trim()).ToArray();
        
        return new[]
        {
            new ParsedClause(
                Type: "ORDER_BY",
                RawSql: orderText,
                Columns: columns,
                Tables: Array.Empty<string>(),
                Complexity: 1
            )
        };
    }

    private static string[] ExtractColumnsFromPredicate(string predicate)
    {
        var matches = Regex.Matches(predicate, @"[\w]+\.[\w]+|[\w]+");
        return matches.Cast<Match>()
            .Select(m => m.Value)
            .Where(v => !v.Contains(".") || v.Split('.').Length == 2)
            .ToArray();
    }
}

