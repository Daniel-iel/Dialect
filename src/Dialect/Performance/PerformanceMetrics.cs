namespace Dialect.Core.Performance;

/// <summary>
/// Performance metrics extracted from a query execution plan.
/// Includes cost analysis, row statistics, and operation classification.
/// </summary>
public record PerformanceMetrics(
    /// <summary>Total cost of the query execution</summary>
    decimal TotalCost,
    
    /// <summary>Number of table scans in the execution plan</summary>
    int TableScanCount,
    
    /// <summary>Number of index seeks in the execution plan</summary>
    int IndexSeekCount,
    
    /// <summary>Number of index scans in the execution plan</summary>
    int IndexScanCount,
    
    /// <summary>Number of nested loop joins</summary>
    int NestedLoopJoinCount,
    
    /// <summary>Number of hash joins</summary>
    int HashJoinCount,
    
    /// <summary>Number of sort operations</summary>
    int SortOperationCount,
    
    /// <summary>Number of filter operations</summary>
    int FilterOperationCount,
    
    /// <summary>Total rows examined across all operations</summary>
    long TotalRowsExamined,
    
    /// <summary>Total rows produced by the query</summary>
    long TotalRowsProduced,
    
    /// <summary>Estimated selectivity (rows produced / rows examined)</summary>
    double Selectivity,
    
    /// <summary>True if plan contains a table scan (potential inefficiency)</summary>
    bool HasTableScan,
    
    /// <summary>True if plan contains a sort operation (potential inefficiency)</summary>
    bool HasSort,
    
    /// <summary>True if plan suggests nested loop with high row count (potential inefficiency)</summary>
    bool HasIneffectiveNestedLoop,
    
    /// <summary>Execution time in milliseconds</summary>
    double ExecutionTimeMs,
    
    /// <summary>Missing index recommendations based on plan analysis</summary>
    IReadOnlyList<string> MissingIndexRecommendations,
    
    /// <summary>Query optimization opportunities</summary>
    IReadOnlyList<string> OptimizationTips
);
