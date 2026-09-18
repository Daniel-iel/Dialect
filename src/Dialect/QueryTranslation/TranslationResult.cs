namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// Result of SQL translation from source to target dialect.
/// Captures both successful compilations and errors/partial results.
/// </summary>
public sealed class TranslationResult
{
    /// <summary>
    /// The compiled query in target dialect, if translation was fully successful.
    /// Null if translation failed or produced only a partial result.
    /// </summary>
    public CompiledQuery? Compiled { get; init; }

    /// <summary>
    /// The detected source SQL provider (SqlServer, PostgreSql, MySql).
    /// Used for debugging and audit trails.
    /// </summary>
    public SqlProvider DetectedSourceProvider { get; init; }

    /// <summary>
    /// Constructs in the source SQL that cannot be translated to the target dialect.
    /// Empty list indicates full translation compatibility.
    /// </summary>
    public IReadOnlyList<string> UntranslatableConstructs { get; init; } = [];

    /// <summary>
    /// Error message if translation failed (e.g., parse error, unsupported syntax).
    /// Null if translation succeeded or partially succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indicates whether the entire SQL was successfully translated.
    /// True only if Compiled is not null and UntranslatableConstructs is empty.
    /// </summary>
    public bool IsFullyTranslated =>
        Compiled is not null && UntranslatableConstructs.Count == 0;

    /// <summary>
    /// Indicates whether translation produced any usable result (full or partial).
    /// True if Compiled is not null, even if UntranslatableConstructs exist.
    /// </summary>
    public bool HasCompiledResult => Compiled is not null;
}
