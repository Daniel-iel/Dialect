namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// Abstract base class for SQL parser adapters for different dialects.
/// Subclasses implement dialect-specific parsing and untranslatable construct detection.
/// Parser adapters are stateless and thread-safe (singleton pattern).
/// </summary>
public abstract class SqlParserAdapter
{
    /// <summary>
    /// Parses SQL source string into an abstract syntax tree (SelectStatement).
    /// Returns null if parsing fails or is not implemented for this dialect.
    /// </summary>
    /// <param name="sql">The SQL source to parse.</param>
    /// <returns>Parsed SelectStatement, or null if parsing fails.</returns>
    public abstract SelectStatement? ParseToAst(string sql);

    /// <summary>
    /// Detects SQL constructs that cannot be translated to other dialects.
    /// Used to flag compatibility issues before attempting translation.
    /// </summary>
    /// <param name="sql">The SQL source to analyze.</param>
    /// <returns>List of untranslatable construct descriptions (may be empty).</returns>
    public abstract IReadOnlyList<string> DetectUntranslatableConstructs(string sql);
}
