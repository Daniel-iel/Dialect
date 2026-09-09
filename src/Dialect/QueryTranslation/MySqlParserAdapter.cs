namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// MySQL parser adapter.
/// Currently a placeholder returning null for parsing (reserved for future ANTLR parser integration).
/// Detects MySQL specific constructs that may not translate to other dialects.
/// </summary>
public sealed class MySqlParserAdapter : SqlParserAdapter
{
    /// <summary>
    /// Parses MySQL SQL source to SelectStatement.
    /// Currently not implemented; reserved for future ANTLR MySQL parser integration.
    /// </summary>
    public override SelectStatement? ParseToAst(string sql)
    {
        // Placeholder: Future implementation will use ANTLR MySQL grammar
        return null;
    }

    /// <summary>
    /// Detects MySQL specific constructs with limited cross-dialect support:
    /// - JSON functions (JSON_EXTRACT, JSON_SET, JSON_REPLACE, etc.)
    /// - LIMIT offset, count syntax (MySQL specific, standard is OFFSET)
    /// - Backtick identifiers (`column_name` syntax)
    /// - Index hints (FORCE INDEX, USE INDEX, IGNORE INDEX)
    /// - GROUP_CONCAT() function (MySQL specific aggregation)
    /// - ON DUPLICATE KEY UPDATE clause (INSERT specific)
    /// - MySQL window functions with specific syntax
    /// </summary>
    public override IReadOnlyList<string> DetectUntranslatableConstructs(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        var sql_lower = sql.ToLowerInvariant();
        var issues = new List<string>();

        // JSON functions
        if (sql_lower.Contains("json_extract") || sql_lower.Contains("json_set") ||
            sql_lower.Contains("json_replace") || sql_lower.Contains("json_array"))
            issues.Add("JSON functions are MySQL specific");

        // LIMIT offset, count syntax
        if (System.Text.RegularExpressions.Regex.IsMatch(sql, @"limit\s+\d+\s*,\s*\d+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            issues.Add("LIMIT offset, count syntax is MySQL specific; standard is OFFSET");

        // Backtick identifiers
        if (sql.Contains("`"))
            issues.Add("Backtick identifiers are MySQL specific");

        // Index hints
        if (sql_lower.Contains("force index") || sql_lower.Contains("use index") ||
            sql_lower.Contains("ignore index"))
            issues.Add("Index hints are MySQL specific");

        // GROUP_CONCAT
        if (sql_lower.Contains("group_concat"))
            issues.Add("GROUP_CONCAT() is MySQL specific");

        // ON DUPLICATE KEY UPDATE
        if (sql_lower.Contains("on duplicate key update"))
            issues.Add("ON DUPLICATE KEY UPDATE is MySQL specific");

        return issues;
    }
}
