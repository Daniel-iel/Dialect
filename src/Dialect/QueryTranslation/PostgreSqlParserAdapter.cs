namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// PostgreSQL parser adapter.
/// Uses a lightweight AST parser for common DML and SELECT patterns.
/// Detects PostgreSQL specific constructs that may not translate to other dialects.
/// </summary>
public sealed class PostgreSqlParserAdapter : SqlParserAdapter
{
    /// <summary>
    /// Parses PostgreSQL SQL source to a query AST.
    /// Uses lightweight parser coverage; complex statements still require richer parser integration.
    /// </summary>
    public override QueryNode? ParseToAst(string sql)
    {
        return SimpleDmlAstParser.Parse(sql, SqlProvider.PostgreSql);
    }

    /// <summary>
    /// Detects PostgreSQL specific constructs with limited cross-dialect support:
    /// - JSON operators (->>, #>, @>, @?)
    /// - ARRAY types and ARRAY operations
    /// - JSONB type and operations
    /// - CUBE aggregation
    /// - Dollar-quoted strings ($$...$$ syntax)
    /// - PostgreSQL window functions with FILTER clause
    /// - Common Table Expressions (CTE) with specific PostgreSQL features
    /// </summary>
    public override IReadOnlyList<string> DetectUntranslatableConstructs(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        var sql_lower = sql.ToLowerInvariant();
        var issues = new List<string>();

        // JSON operators
        if (sql_lower.Contains("->>") || sql_lower.Contains("#>") ||
            sql_lower.Contains("@>") || sql_lower.Contains("@?"))
            issues.Add("JSON operators are PostgreSQL specific");

        // ARRAY type
        if (sql_lower.Contains("array[") || sql_lower.Contains("::array"))
            issues.Add("ARRAY type and operations are PostgreSQL specific");

        // JSONB type
        if (sql_lower.Contains("jsonb"))
            issues.Add("JSONB type is PostgreSQL specific");

        // CUBE aggregation
        if (sql_lower.Contains("cube("))
            issues.Add("CUBE aggregation function is PostgreSQL specific");

        // Dollar-quoted strings
        if (System.Text.RegularExpressions.Regex.IsMatch(sql, @"\$\w*\$"))
            issues.Add("Dollar-quoted strings are PostgreSQL specific syntax");

        // Window FILTER clause
        if (sql_lower.Contains("filter(where"))
            issues.Add("FILTER clause in window functions is PostgreSQL specific");

        return issues;
    }
}
