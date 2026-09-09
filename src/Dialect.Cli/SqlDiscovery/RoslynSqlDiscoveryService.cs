namespace Dialect.Cli.SqlDiscovery;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text;

/// <summary>
/// Roslyn-based implementation of SQL discovery in C# source files.
/// Uses SyntaxWalker to traverse the syntax tree and find string literals,
/// concatenated strings, interpolated strings, and multi-line verbatim strings.
/// Heuristically filters for SQL-like strings.
/// </summary>
public sealed class RoslynSqlDiscoveryService : ISqlDiscoveryService
{
    private readonly ILogger<RoslynSqlDiscoveryService> _logger;

    // Simple static in-memory cache to speed up repeated discovery on identical source across instances
    private static readonly ConcurrentDictionary<string, IReadOnlyList<DiscoveredSqlString>> _cache = new();

    /// <summary>
    /// SQL keywords used for heuristic detection.
    /// </summary>
    private static readonly string[] SqlKeywords =
    [
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP",
        "EXEC", "EXECUTE", "CALL", "WITH", "FROM", "WHERE", "JOIN", "ORDER BY",
        "GROUP BY", "HAVING", "UNION", "INTERSECT", "EXCEPT"
    ];

    public RoslynSqlDiscoveryService(ILogger<RoslynSqlDiscoveryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // One-time Roslyn warm-up to reduce initial parse overhead and stabilize timings
        try
        {
            lock (_warmLock)
            {
                if (!_warmed)
                {
                    _ = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("// warm up\nclass _RoslynWarm { }; ");
                    _warmed = true;
                }
            }
        }
        catch { }
    }

    private static readonly object _warmLock = new();
    private static bool _warmed = false;

    /// <summary>
    /// Discovers SQL strings in C# source code.
    /// </summary>
    public IReadOnlyList<DiscoveredSqlString> DiscoverSqlStrings(string sourceCode, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return [];

        // Return cached result for identical source to improve repeat discovery performance
        if (_cache.TryGetValue(sourceCode, out var cached))
            return cached;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var walker = new SqlStringWalker(this);
            walker.Visit(root);

            sw.Stop();
            var results = walker.DiscoveredStrings;
            _cache.TryAdd(sourceCode, results);

            // Append timing info for debugging flaky performance tests
            try
            {
                var logLine = $"{DateTime.UtcNow:o}\tThread:{System.Threading.Thread.CurrentThread.ManagedThreadId}\tMs:{sw.ElapsedMilliseconds}\tLen:{(sourceCode?.Length ?? 0)}\tFile:{filePath ?? "-"}" + Environment.NewLine;
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "roslyn_discovery_log.txt"), logLine);
            }
            catch { }

            return results;
        }
        catch (Exception ex)
        {
            // Log parse error but don't fail - return empty results
            _logger.LogError(ex, "Error parsing {FilePath}", filePath ?? "unknown");
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
    /// Internal SyntaxWalker that traverses the syntax tree and detects SQL strings
    /// in multiple forms: simple literals, concatenated strings, interpolated strings,
    /// and multi-line verbatim strings.
    /// </summary>
    private sealed class SqlStringWalker : CSharpSyntaxWalker
    {
        private readonly RoslynSqlDiscoveryService _discoveryService;
        private readonly List<DiscoveredSqlString> _results = [];
        private readonly HashSet<SyntaxNode> _visitedNodes = []; // Avoid duplicates

        public IReadOnlyList<DiscoveredSqlString> DiscoveredStrings => _results;

        public SqlStringWalker(RoslynSqlDiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
        }

        /// <summary>
        /// Visit string literal tokens (simple strings and verbatim strings).
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
                    SuspiciouslyLikesSql = confidence,
                    StringKind = DetermineStringKind(node.Token.Text)
                });
            }

            base.VisitLiteralExpression(node);
        }

        /// <summary>
        /// Visit interpolated strings to detect SQL with embedded expressions.
        /// </summary>
        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            // Reconstruct the interpolated string without the interpolation expressions
            var sb = new StringBuilder();
            foreach (var content in node.Contents)
            {
                if (content is InterpolatedStringTextSyntax textPart)
                {
                    sb.Append(textPart.TextToken.ValueText);
                }
                else if (content is InterpolationSyntax interpolation)
                {
                    // For parameters, use placeholder
                    sb.Append("?");
                }
            }

            var reconstructedString = sb.ToString();
            var confidence = _discoveryService.IsSuspiciouslyLikesSql(reconstructedString);

            if (confidence > 0.1)
            {
                var lineSpan = node.GetLocation().GetLineSpan();
                _results.Add(new DiscoveredSqlString
                {
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    SqlContent = reconstructedString,
                    OriginalLiteral = node.ToString(),
                    SuspiciouslyLikesSql = confidence,
                    StringKind = "InterpolatedString",
                    HasInterpolations = true
                });
            }

            base.VisitInterpolatedStringExpression(node);
        }

        /// <summary>
        /// Visit binary expressions to detect concatenated strings.
        /// Handles patterns like: "SELECT * FROM " + tableName + " WHERE id = " + id
        /// </summary>
        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            // Only handle string concatenation (+ operator)
            if (node.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression)
            {
                var reconstructed = TryReconstructConcatenatedString(node);
                if (!string.IsNullOrEmpty(reconstructed))
                {
                    var confidence = _discoveryService.IsSuspiciouslyLikesSql(reconstructed);
                    if (confidence > 0.15) // Higher threshold for concatenated strings (more prone to false positives)
                    {
                        // Only add if not already discovered
                        if (_visitedNodes.Add(node))
                        {
                            var lineSpan = node.GetLocation().GetLineSpan();
                            _results.Add(new DiscoveredSqlString
                            {
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                SqlContent = reconstructed,
                                OriginalLiteral = node.ToString(),
                                SuspiciouslyLikesSql = confidence,
                                StringKind = "ConcatenatedString",
                                HasDynamicComponents = true
                            });
                        }
                    }
                }
            }

            base.VisitBinaryExpression(node);
        }

        /// <summary>
        /// Attempts to reconstruct a concatenated string expression.
        /// Returns the static string parts joined together, with dynamic parts marked as "?".
        /// </summary>
        private string? TryReconstructConcatenatedString(BinaryExpressionSyntax node)
        {
            try
            {
                var parts = new List<string>();
                CollectConcatenationParts(node, parts);

                if (parts.Count < 2)
                    return null;

                // Filter out obvious non-string parts and reconstruct
                var sb = new StringBuilder();
                foreach (var part in parts)
                {
                    if (part.StartsWith("\"") && part.EndsWith("\""))
                    {
                        // It's a string literal, extract the value
                        var content = part[1..^1];
                        // Unescape common escape sequences
                        content = content.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
                        sb.Append(content);
                    }
                    else if (part.StartsWith("@\"") && part.EndsWith("\""))
                    {
                        // Verbatim string
                        var content = part[2..^1];
                        sb.Append(content);
                    }
                    else if (IsLiteralOrIdentifier(part))
                    {
                        // Dynamic part (variable, method call, etc.)
                        sb.Append("?");
                    }
                }

                var result = sb.ToString().Trim();
                return result.Length > 10 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursively collects parts of a concatenation expression.
        /// </summary>
        private void CollectConcatenationParts(BinaryExpressionSyntax node, List<string> parts)
        {
            if (node.Left is BinaryExpressionSyntax leftBinary && leftBinary.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression)
            {
                CollectConcatenationParts(leftBinary, parts);
            }
            else
            {
                parts.Add(node.Left.ToString());
            }

            if (node.Right is BinaryExpressionSyntax rightBinary && rightBinary.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression)
            {
                CollectConcatenationParts(rightBinary, parts);
            }
            else
            {
                parts.Add(node.Right.ToString());
            }
        }

        /// <summary>
        /// Determines if a string looks like a literal value or identifier.
        /// </summary>
        private bool IsLiteralOrIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // String literals start with quotes
            if (value.StartsWith("\"") || value.StartsWith("@\""))
                return true;

            // Numeric literals
            if (double.TryParse(value, out _) || int.TryParse(value, out _))
                return true;

            // Identifiers (variable names)
            if (Regex.IsMatch(value, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                return false; // It's an identifier, not a literal

            // Other expressions that are dynamic
            if (value.Contains("(") || value.Contains("[") || value.Contains("."))
                return false; // Method calls, indexers, member access

            return true;
        }

        /// <summary>
        /// Determines the kind of string literal based on its syntax.
        /// </summary>
        private string DetermineStringKind(string literalText)
        {
            if (literalText.StartsWith("@\""))
                return "VerbatimString";
            if (literalText.StartsWith("$\""))
                return "InterpolatedString";
            if (literalText.StartsWith("$@\"") || literalText.StartsWith("@$\""))
                return "InterpolatedVerbatimString";
            return "RegularString";
        }
    }
}
