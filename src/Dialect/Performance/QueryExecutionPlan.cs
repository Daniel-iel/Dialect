namespace Dialect.Core.Performance;

/// <summary>
/// Represents a single operation in the execution plan tree.
/// Each node corresponds to a table scan, index seek, join, sort, etc.
/// </summary>
public record ExecutionPlanNode(
    /// <summary>Type of operation (TableScan, IndexSeek, NestedLoopJoin, etc.)</summary>
    string OperationType,

    /// <summary>Estimated or actual number of rows output by this operation</summary>
    long RowsProduced,

    /// <summary>Estimated or actual number of rows examined/processed</summary>
    long RowsExamined,

    /// <summary>Cost estimate for this operation (dialect-specific units)</summary>
    decimal Cost,

    /// <summary>Table or index name if applicable</summary>
    string? ObjectName,

    /// <summary>Predicate/filter applied at this level if any</summary>
    string? Predicate,

    /// <summary>Child operations in the plan tree</summary>
    IReadOnlyList<ExecutionPlanNode> Children,

    /// <summary>Dialect-specific node attributes</summary>
    IReadOnlyDictionary<string, object> Properties
);

/// <summary>
/// Represents a query execution plan parsed from database-specific plan output.
/// Contains hierarchical node information with costs and statistics.
/// </summary>
public record QueryExecutionPlan(
    /// <summary>Root node of the execution plan tree</summary>
    ExecutionPlanNode RootNode,

    /// <summary>Total estimated or actual cost of the query</summary>
    decimal TotalCost,

    /// <summary>Number of rows returned by the query</summary>
    long TotalRowsProduced,

    /// <summary>Execution time in milliseconds</summary>
    double ExecutionTimeMs,

    /// <summary>Query string that produced this plan</summary>
    string QueryText,

    /// <summary>Dialect-specific plan metadata</summary>
    IReadOnlyDictionary<string, object> Metadata
);
