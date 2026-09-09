namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// SQL Server (T-SQL) parser adapter.
/// Uses a lightweight AST parser for common DML and SELECT patterns.
/// Detects T-SQL specific constructs that may not translate to other dialects.
/// </summary>
public sealed class SqlServerParserAdapter : SqlParserAdapter
{
    /// <summary>
    /// Parses T-SQL source to a query AST.
    /// Uses lightweight parser coverage; complex statements still require richer parser integration.
    /// </summary>
    public override QueryNode? ParseToAst(string sql)
    {
        return SimpleDmlAstParser.Parse(sql, SqlProvider.SqlServer);
    }

    /// <summary>
    /// Detects T-SQL constructs with limited cross-dialect support:
    /// - MERGE statements
    /// - FORMAT() function (SQL Server specific)
    /// - XML processing (nodes(), value(), query())
    /// - TOP clause without ORDER BY
    /// - OUTPUT clause (INSERT/UPDATE/DELETE)
    /// - Variable references (@variable)
    /// </summary>
    public override IReadOnlyList<string> DetectUntranslatableConstructs(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        var sql_lower = sql.ToLowerInvariant();
        var issues = new List<string>();

        if (sql_lower.Contains("merge "))
            issues.Add("MERGE statement not supported in target dialect");

        if (sql_lower.Contains("format("))
            issues.Add("FORMAT() function is SQL Server specific");

        if (sql_lower.Contains(".nodes(") || sql_lower.Contains(".value(") || sql_lower.Contains(".query("))
            issues.Add("XML processing (nodes/value/query) not supported");

        if (sql_lower.Contains(" top ") && !sql_lower.Contains("order by"))
            issues.Add("TOP clause without ORDER BY may not translate correctly");

        if (sql_lower.Contains("output "))
            issues.Add("OUTPUT clause is SQL Server specific");

        if (System.Text.RegularExpressions.Regex.IsMatch(sql, @"@\w+"))
            issues.Add("Variable references (@variable) may not translate");

        return issues;
    }
}
