namespace Dialect.Core.QueryTranslation;

using System;
using System.Collections.Generic;
using Dialect.Core.AST;
using Dialect.Core.Parsing;

/// <summary>
/// SQL parser adapter using Antlr-based grammar.
/// Provides robust parsing for all SQL dialects using a unified grammar.
/// Fallback implementation that can be used alongside dialect-specific parsers.
/// </summary>
public sealed class AntlrSqlParserAdapter : SqlParserAdapter
{
    private readonly AntlrSqlParser _parser;
    private readonly SqlProvider _sourceDialect;

    public AntlrSqlParserAdapter(SqlProvider sourceDialect)
    {
        _parser = new AntlrSqlParser();
        _sourceDialect = sourceDialect;
    }

    /// <summary>
    /// Parses SQL using Antlr grammar.
    /// </summary>
    public override SelectStatement? ParseToAst(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return null;
        }

        try
        {
            var statement = _parser.Parse(sql, _sourceDialect);
            
            if (statement is SelectStatement selectStatement)
            {
                return selectStatement;
            }

            // If parsing returns non-SELECT statement, return null
            // (SelectStatement? return type requires SELECT)
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AntlrSqlParserAdapter failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detects untranslatable SQL constructs.
    /// Flags patterns that would fail during transformation.
    /// </summary>
    public override IReadOnlyList<string> DetectUntranslatableConstructs(string sql)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(sql))
        {
            return issues;
        }

        var upperSql = sql.ToUpperInvariant();

        // Detect dynamic SQL (string interpolation patterns)
        if (sql.Contains("${") || sql.Contains("$\"") || sql.Contains("\"$") || sql.Contains("+ \"") || sql.Contains("+ '"))
        {
            issues.Add("Dynamic SQL (string interpolation/concatenation) detected - manual review required");
        }

        // Detect CLR procedures (SQL Server specific)
        if (upperSql.Contains("EXTERNAL NAME"))
        {
            issues.Add("CLR procedure detected - not translatable, requires rewrite");
        }

        // Detect complex cursors
        if (upperSql.Contains("DECLARE") && upperSql.Contains("CURSOR"))
        {
            issues.Add("Cursor construct detected - complex logic may not translate automatically");
        }

        // Detect JSON operations with dialect-specific syntax
        if (upperSql.Contains("JSON_VALUE") || upperSql.Contains("JSON_EXTRACT") || 
            upperSql.Contains("JSONB") || upperSql.Contains("->") || upperSql.Contains("->>"))
        {
            issues.Add("JSON operations with dialect-specific syntax - may require manual translation");
        }

        // Detect FULLTEXT search (MySQL specific)
        if (upperSql.Contains("FULLTEXT") || upperSql.Contains("MATCH") && upperSql.Contains("AGAINST"))
        {
            issues.Add("FULLTEXT search syntax detected - not universally supported");
        }

        // Detect MERGE without clear destination
        if (upperSql.Contains("MERGE INTO") && !upperSql.Contains("WHEN MATCHED"))
        {
            issues.Add("MERGE statement without WHEN clauses - may be incomplete");
        }

        // Detect recursive CTEs with CYCLE (PostgreSQL)
        if (upperSql.Contains("WITH RECURSIVE") && upperSql.Contains("CYCLE"))
        {
            issues.Add("Recursive CTE with CYCLE detected - CYCLE syntax not universally supported");
        }

        // Detect stored procedures with complex logic
        if (upperSql.Contains("CREATE PROCEDURE") || upperSql.Contains("CREATE FUNCTION"))
        {
            if (upperSql.Contains("BEGIN") && upperSql.Contains("END"))
            {
                issues.Add("Stored procedure/function with complex logic - may require manual translation");
            }
        }

        // Detect XML operations (SQL Server specific)
        if (upperSql.Contains(".value") || upperSql.Contains(".exist") || upperSql.Contains(".nodes"))
        {
            issues.Add("XML operations (SQL Server specific syntax) - requires manual translation");
        }

        return issues;
    }

    /// <summary>
    /// Get dialect-specific warnings about parsing limitations.
    /// </summary>
    public IReadOnlyList<string> GetParsingLimitations()
    {
        return new List<string>
        {
            "Antlr parser uses simplified grammar - complex edge cases may not parse",
            "Multi-line string literals must be collapsed before parsing",
            "Concatenated strings are not automatically reconstructed"
        };
    }
}
