namespace Dialect.Core.Versioning;

/// <summary>
/// Defines SQL features supported by a specific database version.
/// Used to tailor query generation to version-specific capabilities.
/// </summary>
public record VersionCapabilities(
    bool SupportsWindowFunctions,
    bool SupportsCTEs,
    bool SupportsUpsert,
    bool SupportsJsonFunctions,
    bool SupportsFullTextSearch,
    bool SupportsPartitioning,
    bool SupportsGeneratedColumns,
    bool SupportsCommonTableExpressions,
    bool SupportsPartialIndexes,
    bool SupportsRecursiveCTEs
)
{
    /// <summary>
    /// Checks if all required capabilities are supported.
    /// </summary>
    public bool Supports(params string[] capabilities)
    {
        return capabilities.All(cap => cap switch
        {
            "WINDOW_FUNCTIONS" => SupportsWindowFunctions,
            "CTES" => SupportsCTEs,
            "UPSERT" => SupportsUpsert,
            "JSON" => SupportsJsonFunctions,
            "FULLTEXT" => SupportsFullTextSearch,
            "PARTITIONING" => SupportsPartitioning,
            "GENERATED_COLUMNS" => SupportsGeneratedColumns,
            "COMMON_TABLE_EXPRESSIONS" => SupportsCommonTableExpressions,
            "PARTIAL_INDEXES" => SupportsPartialIndexes,
            "RECURSIVE_CTES" => SupportsRecursiveCTEs,
            _ => throw new ArgumentException($"Unknown capability: {cap}")
        });
    }
}
