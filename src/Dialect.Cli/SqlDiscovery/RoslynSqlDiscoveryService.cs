namespace Dialect.Cli.SqlDiscovery;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

/// <summary>
/// Roslyn-based implementation of SQL discovery in C# source files.
/// Uses SyntaxWalker to traverse the syntax tree and find string literals.
/// Heuristically filters for SQL-like strings.
/// </summary>
public sealed class RoslynSqlDiscoveryService : ISqlDiscoveryService
{
    /// <summary>
    /// SQL keywords used for heuristic detection.
    /// </summary>
    private static readonly string[] SqlKeywords =
    [
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP",
        "EXEC", "EXECUTE", "CALL", "WITH", "FROM", "WHERE", "JOIN", "ORDER BY",
        "GROUP BY", "HAVING", "UNION", "INTERSECT", "EXCEPT"
    ];

    /// <summary>
    /// Discovers SQL strings in C# source code.
    /// </summary>
    public IReadOnlyList<DiscoveredSqlString> DiscoverSqlStrings(string sourceCode, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return [];

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var walker = new SqlStringWalker(this);
            walker.Visit(root);

            return walker.DiscoveredStrings;
        }
        catch (Exception ex)
        {
            // Log parse error but don't fail - return empty results
            Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Heuristically scores whether a string looks like SQL.
    /// Returns confidence between 0.0 (definitely not SQL) and 1.0 (definitely SQL).
    /// </summary>
    public double IsSuspiciouslyLikesSql(string potentialSql)
    {
        if (string.IsNullOrWhiteSpace(potentialSql))
            return 0.0;

        var sql = potentialSql.ToUpperInvariant().Trim();

        // Minimum length check
        if (sql.Length < 10)
            return 0.0;

        var score = 0.0;

        // Check for SQL keywords (strong signal)
        var matchedKeywords = SqlKeywords.Count(kw => sql.Contains(kw));
        if (matchedKeywords > 0)
        {
            score += Math.Min(0.8, matchedKeywords * 0.2);
        }

        // Check for common SQL patterns
        if (Regex.IsMatch(sql, @"\bFROM\s+\w+", RegexOptions.IgnoreCase))
            score += 0.1;

        if (Regex.IsMatch(sql, @"\bWHERE\s+\w+\s*=", RegexOptions.IgnoreCase))
            score += 0.1;

        if (Regex.IsMatch(sql, @"@\w+|\?|\$\w+")) // Parameters
            score += 0.1;

        // Check for SQL-like structure (word patterns)
        var words = Regex.Split(sql, @"\s+");
        if (words.Length > 5)
            score += 0.05;

        return Math.Min(1.0, score);
    }

    /// <summary>
    /// Internal SyntaxWalker that traverses the syntax tree.
    /// </summary>
    private sealed class SqlStringWalker : CSharpSyntaxWalker
    {
        private readonly RoslynSqlDiscoveryService _discoveryService;
        private readonly List<DiscoveredSqlString> _results = [];

        public IReadOnlyList<DiscoveredSqlString> DiscoveredStrings => _results;

        public SqlStringWalker(RoslynSqlDiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
        }

        /// <summary>
        /// Visit string literal tokens.
        /// </summary>
        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            // Only process string literals
            if (node.Token.ValueText is not string stringValue)
            {
                base.VisitLiteralExpression(node);
                return;
            }

            // Check if this looks like SQL
            var confidence = _discoveryService.IsSuspiciouslyLikesSql(stringValue);
            if (confidence > 0.1) // Threshold: at least some SQL-like characteristics
            {
                var lineSpan = node.GetLocation().GetLineSpan();
                _results.Add(new DiscoveredSqlString
                {
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    SqlContent = stringValue,
                    OriginalLiteral = node.Token.Text,
                    SuspiciouslyLikesSql = confidence
                });
            }

            base.VisitLiteralExpression(node);
        }
    }
}
