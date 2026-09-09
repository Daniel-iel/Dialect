using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;

namespace Dialect.Core.Parsing
{
    /// <summary>
    /// SQL Parser Adapter for Antlr-based parsing.
    /// Implements untranslatable construct detection and delegates parsing to AntlrSqlParser.
    /// </summary>
    public class AntlrSqlParserAdapter : SqlParserAdapter
    {
        private readonly ISqlParser _sqlParser;

        public AntlrSqlParserAdapter()
        {
            _sqlParser = new AntlrSqlParser();
        }

        /// <summary>
        /// Parse SQL string to AST.
        /// Delegates to AntlrSqlParser which uses keyword-based extraction.
        /// </summary>
        public override SelectStatement? ParseToAst(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return null;

            // Auto-detect dialect for proper parsing
            var dialect = SqlDialectDetector.DetectDialectProvider(sql);
            if (!dialect.HasValue)
                dialect = SqlProvider.SqlServer; // Default fallback

            return _sqlParser.Parse(sql, dialect.Value);
        }

        /// <summary>
        /// Detect SQL constructs that cannot be translated to other dialects.
        /// Scans for dynamic SQL, stored procedures, cursors, XML operations, FULLTEXT, etc.
        /// </summary>
        public override IReadOnlyList<string> DetectUntranslatableConstructs(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return Array.Empty<string>();

            var issues = new List<string>();
            var upperSql = sql.ToUpper();

            // Dynamic SQL patterns
            DetectDynamicSql(sql, upperSql, issues);

            // Stored procedures and functions
            DetectStoredProcedures(upperSql, issues);

            // Cursor operations
            DetectCursors(upperSql, issues);

            // JSON operations (dialect-specific)
            DetectJsonOperations(sql, upperSql, issues);

            // FULLTEXT search
            if (Regex.IsMatch(upperSql, @"\b(MATCH|AGAINST|CONTAINS|FREETEXT)\b", RegexOptions.IgnoreCase))
                issues.Add("FULLTEXT search (MATCH/AGAINST/CONTAINS) not supported");

            // XML operations
            if (Regex.IsMatch(upperSql, @"\.value\(|\.exist\(|\.nodes\(|\.query\(", RegexOptions.IgnoreCase))
                issues.Add("XML operations (.value, .exist, .nodes, .query) not supported");

            // MERGE statement complications
            DetectMergeIssues(upperSql, issues);

            // Recursive CTEs with CYCLE (PostgreSQL 14+)
            if (Regex.IsMatch(upperSql, @"\bWITH\s+RECURSIVE.*\bCYCLE\b", RegexOptions.IgnoreCase))
                issues.Add("Recursive CTE with CYCLE clause not supported");

            // Window frame exclusion (PostgreSQL-specific)
            if (Regex.IsMatch(upperSql, @"\bEXCLUDE\s+(CURRENT ROW|GROUP|TIES|NO OTHERS)\b", RegexOptions.IgnoreCase))
                issues.Add("Window frame EXCLUDE clause not supported");

            // CLR procedures (T-SQL)
            if (Regex.IsMatch(upperSql, @"\bEXTERNAL\s+NAME\b", RegexOptions.IgnoreCase))
                issues.Add("CLR procedures (EXTERNAL NAME) not supported");

            // Multi-statement transactions
            DetectTransactions(upperSql, issues);

            return issues;
        }

        private void DetectDynamicSql(string sql, string upperSql, List<string> issues)
        {
            // String interpolation patterns
            if (Regex.IsMatch(sql, @"\$""[^""]*{[^}]*}[^""]*""", RegexOptions.IgnoreCase))
                issues.Add("Dynamic SQL via string interpolation ($\"...\") not supported");

            if (Regex.IsMatch(sql, @"\$""[^""]*@[^""]*""", RegexOptions.IgnoreCase))
                issues.Add("Dynamic SQL via string interpolation ($@\"\") not supported");

            // String concatenation with +
            if (Regex.IsMatch(upperSql, @"'[^']*'\s*\+\s*'[^']*'") || 
                Regex.IsMatch(upperSql, @"""[^""]*""\s*\+\s*""[^""]*"""))
                issues.Add("Dynamic SQL via string concatenation not supported");

            // String concatenation with || (PostgreSQL/MySQL)
            if (Regex.IsMatch(upperSql, @"'[^']*'\s*\|\|\s*'[^']*'"))
                issues.Add("Dynamic SQL via || operator concatenation not supported");

            // Variable assignment patterns
            if (Regex.IsMatch(upperSql, @"@\w+\s*=\s*N?'") || 
                Regex.IsMatch(upperSql, @"@\w+\s*=\s*\(SELECT"))
                issues.Add("Dynamic SQL via variable assignment not supported");

            // EXEC/EXECUTE of variables or strings
            if (Regex.IsMatch(upperSql, @"\b(EXEC|EXECUTE)\s+(@|\(|N?'|"")", RegexOptions.IgnoreCase))
                issues.Add("Dynamic SQL via EXEC/EXECUTE not supported");
        }

        private void DetectStoredProcedures(string upperSql, List<string> issues)
        {
            if (Regex.IsMatch(upperSql, @"\bCREATE\s+PROCEDURE\b") || 
                Regex.IsMatch(upperSql, @"\bCREATE\s+FUNCTION\b"))
                issues.Add("Stored procedures and functions (CREATE PROCEDURE/FUNCTION) not supported");

            if (Regex.IsMatch(upperSql, @"\bALTER\s+PROCEDURE\b") || 
                Regex.IsMatch(upperSql, @"\bALTER\s+FUNCTION\b"))
                issues.Add("Stored procedures and functions (ALTER PROCEDURE/FUNCTION) not supported");

            // Stored procedure calls with complex parameters
            if (Regex.IsMatch(upperSql, @"\bEXEC\s+\w+\s+@\w+\s*=\s*\(SELECT", RegexOptions.IgnoreCase))
                issues.Add("Complex stored procedure calls (with subquery parameters) not supported");

            // Simple EXEC calls should also be flagged (e.g., EXEC sp_GetUsers @Status = 'Active')
            if (Regex.IsMatch(upperSql, @"\bEXEC\b|\bEXECUTE\b", RegexOptions.IgnoreCase))
                issues.Add("Stored procedure execution (EXEC/EXECUTE) detected — review manually");
        }

        private void DetectCursors(string upperSql, List<string> issues)
        {
            if (Regex.IsMatch(upperSql, @"\bDECLARE\s+\w+\s+CURSOR\b", RegexOptions.IgnoreCase))
                issues.Add("Cursor operations (DECLARE CURSOR) not supported");

            if (Regex.IsMatch(upperSql, @"\b(FETCH|OPEN|CLOSE)\b.*\bCURSOR", RegexOptions.IgnoreCase))
                issues.Add("Cursor operations (FETCH/OPEN/CLOSE) not supported");
        }

        private void DetectJsonOperations(string sql, string upperSql, List<string> issues)
        {
            // SQL Server JSON
            if (Regex.IsMatch(upperSql, @"\bJSON_VALUE\(|JSON_QUERY\(|JSON_MODIFY\(", RegexOptions.IgnoreCase))
                issues.Add("SQL Server JSON functions (JSON_VALUE, JSON_QUERY, JSON_MODIFY) not supported");

            // PostgreSQL JSON/JSONB
            // Match operators: ->, ->> (followed by quoted key), ?, @>, <@, #-, #>
            if (Regex.IsMatch(sql, @"(->|->>['""]|\?|@>|<@|#-|#>)") || 
                Regex.IsMatch(upperSql, @"\bjson(b?_[a-z_]+|_[a-z_]+)\(", RegexOptions.IgnoreCase))
                issues.Add("PostgreSQL JSON/JSONB operators and functions not supported");

            // MySQL JSON
            if (Regex.IsMatch(upperSql, @"\bJSON_\w+\(", RegexOptions.IgnoreCase))
                issues.Add("MySQL JSON functions not supported");
        }

        private void DetectMergeIssues(string upperSql, List<string> issues)
        {
            if (Regex.IsMatch(upperSql, @"\bMERGE\b"))
            {
                // Check if it's a simple MERGE with only INSERT/UPDATE/DELETE
                if (!Regex.IsMatch(upperSql, @"\bWHEN\s+(NOT\s+)?MATCHED\b"))
                    issues.Add("MERGE statement without WHEN clauses not supported");

                // Multiple WHEN clauses (more complex)
                var whenCount = Regex.Matches(upperSql, @"\bWHEN\s+(NOT\s+)?MATCHED\b").Count;
                if (whenCount > 3)
                    issues.Add("MERGE statement with more than 3 WHEN clauses not supported");
            }
        }

        private void DetectTransactions(string upperSql, List<string> issues)
        {
            if (Regex.IsMatch(upperSql, @"\b(BEGIN\s+TRANSACTION|BEGIN\s+TRAN|START\s+TRANSACTION)\b"))
                issues.Add("Explicit transaction control (BEGIN/COMMIT/ROLLBACK) in SQL not supported");

            if (Regex.IsMatch(upperSql, @"\b(COMMIT|ROLLBACK)\b"))
                issues.Add("Explicit transaction control (BEGIN/COMMIT/ROLLBACK) in SQL not supported");
        }
    }
}
