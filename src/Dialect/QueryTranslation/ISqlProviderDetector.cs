namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// Service for heuristically detecting source SQL provider from connection strings.
/// Implements simple pattern matching on connection string keywords.
/// </summary>
public interface ISqlProviderDetector
{
    /// <summary>
    /// Attempts to detect the SQL provider from a connection string.
    /// Uses heuristic pattern matching on connection string contents.
    /// </summary>
    /// <param name="connectionString">The connection string to analyze.</param>
    /// <returns>
    /// The detected SqlProvider, or null if provider cannot be confidently determined.
    /// </returns>
    SqlProvider? Detect(string connectionString);
}
