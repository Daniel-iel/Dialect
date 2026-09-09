namespace Dialect.Cli.SqlDiscovery;

/// <summary>
/// Result of discovering a SQL string in source code.
/// Includes location (line, column), content, and metadata about string type.
/// </summary>
public sealed class DiscoveredSqlString
{
    /// <summary>
    /// Line number (1-based) where SQL was found.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Column number (1-based) where SQL string starts.
    /// </summary>
    public int ColumnNumber { get; init; }

    /// <summary>
    /// The SQL string value (without quotes or dynamic markers).
    /// </summary>
    public required string SqlContent { get; init; }

    /// <summary>
    /// The original C# string literal (with quotes/escapes).
    /// </summary>
    public required string OriginalLiteral { get; init; }

    /// <summary>
    /// Heuristic confidence that this is actually SQL (0.0-1.0).
    /// Based on keywords like SELECT, INSERT, UPDATE, DELETE, etc.
    /// </summary>
    public double SuspiciouslyLikesSql { get; init; }

    /// <summary>
    /// Type of string literal: RegularString, VerbatimString, InterpolatedString, etc.
    /// </summary>
    public string StringKind { get; init; } = "RegularString";

    /// <summary>
    /// Whether this string contains interpolations (e.g., $"SELECT {column}").
    /// </summary>
    public bool HasInterpolations { get; init; }

    /// <summary>
    /// Whether this string is concatenated or has dynamic components.
    /// Indicates manual review may be needed.
    /// </summary>
    public bool HasDynamicComponents { get; init; }

    public override string ToString() =>
        $"Line {LineNumber}:{ColumnNumber} [{StringKind}] - {SqlContent[..Math.Min(50, SqlContent.Length)]}..." +
        $" (Confidence: {SuspiciouslyLikesSql:P})";
}

/// <summary>
/// Service for discovering SQL strings in C# source files using Roslyn.
/// </summary>
public interface ISqlDiscoveryService
{
    /// <summary>
    /// Discovers all SQL strings in a C# source file.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="filePath">Optional file path for context/diagnostics.</param>
    /// <returns>Collection of discovered SQL strings with their locations.</returns>
    IReadOnlyList<DiscoveredSqlString> DiscoverSqlStrings(string sourceCode, string? filePath = null);

    /// <summary>
    /// Heuristically scores whether a string looks like SQL.
    /// Returns a confidence value between 0.0 and 1.0.
    /// </summary>
    double IsSuspiciouslyLikesSql(string potentialSql);
}
