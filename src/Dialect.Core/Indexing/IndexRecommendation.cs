namespace Dialect.Core.Indexing;

/// <summary>
/// Type of database index being recommended.
/// </summary>
public enum IndexType
{
    /// <summary>Single-column index for simple predicates</summary>
    SingleColumn,

    /// <summary>Multi-column index for composite WHERE conditions</summary>
    Composite,

    /// <summary>Index including non-key columns for covering scans</summary>
    Covering,

    /// <summary>Full-text index for text search (MySQL-specific)</summary>
    FullText,

    /// <summary>Filtered/partial index for static predicates</summary>
    Filtered
}

/// <summary>
/// Priority level for index recommendation.
/// </summary>
public enum IndexPriority
{
    /// <summary>Index is essential for query performance</summary>
    Critical,

    /// <summary>Index provides significant performance benefit</summary>
    High,

    /// <summary>Index provides moderate performance benefit</summary>
    Medium,

    /// <summary>Index provides minor performance benefit</summary>
    Low
}

/// <summary>
/// Represents an index recommendation from the advisor.
/// </summary>
public record IndexRecommendation(
    string TableName,
    string[] ColumnNames,
    IndexType Type,
    decimal EstimatedBenefit,  // 0.0 to 1.0, impact on query cost
    string Reasoning,
    IndexPriority Priority
);
