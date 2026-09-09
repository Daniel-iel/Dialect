namespace Dialect.Core.AST;

/// <summary>
/// Frame specification for window functions (ROWS or RANGE).
/// Example: ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING
/// </summary>
public sealed record FrameSpec(
    string FrameType,  // "ROWS" or "RANGE"
    string StartBound, // "UNBOUNDED PRECEDING", "CURRENT ROW", "1 PRECEDING", etc.
    string? EndBound = null  // null means end same as start
)
{
    public string ToSql()
    {
        if (EndBound == null)
            return $"{FrameType} {StartBound}";
        return $"{FrameType} BETWEEN {StartBound} AND {EndBound}";
    }
}

/// <summary>
/// Represents the OVER clause of a window function.
/// Example: OVER (PARTITION BY dept_id ORDER BY salary DESC ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING)
/// </summary>
public sealed record OverClause(
    IReadOnlyList<string>? PartitionByColumns = null,
    IReadOnlyList<OrderByClause>? OrderByItems = null,
    FrameSpec? Frame = null
)
{
    public bool IsEmpty =>
        (PartitionByColumns == null || PartitionByColumns.Count == 0) &&
        (OrderByItems == null || OrderByItems.Count == 0) &&
        Frame == null;
}

/// <summary>
/// Represents a window function call within a SELECT clause.
/// Examples:
///   ROW_NUMBER() OVER (PARTITION BY dept_id ORDER BY salary DESC)
///   SUM(amount) OVER (ORDER BY order_date ROWS BETWEEN 1 PRECEDING AND CURRENT ROW)
///   RANK() OVER (PARTITION BY category ORDER BY sales DESC)
/// </summary>
public sealed record WindowFunction(
    string FunctionName,  // "ROW_NUMBER", "RANK", "DENSE_RANK", "LAG", "LEAD", "SUM", "AVG", etc.
    IReadOnlyList<string>? Args = null,  // Arguments (e.g., column for LAG/LEAD, null for ROW_NUMBER)
    OverClause? Over = null,  // OVER clause specification
    string? Alias = null  // Column alias for the result
)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FunctionName))
            throw new ArgumentException("Function name cannot be empty", nameof(FunctionName));

        // Over clause is required for window functions
        if (Over == null || Over.IsEmpty)
            throw new ArgumentException($"Window function '{FunctionName}' requires an OVER clause", nameof(Over));
    }
}

/// <summary>
/// Represents aggregate and ranking functions that can be used as window functions.
/// </summary>
public static class WindowFunctionNames
{
    // Ranking functions
    public const string RowNumber = "ROW_NUMBER";
    public const string Rank = "RANK";
    public const string DenseRank = "DENSE_RANK";
    public const string NTile = "NTILE";

    // Analytical functions
    public const string Lag = "LAG";
    public const string Lead = "LEAD";
    public const string FirstValue = "FIRST_VALUE";
    public const string LastValue = "LAST_VALUE";
    public const string NthValue = "NTH_VALUE";

    // Aggregate functions (can also be used as window functions)
    public const string Sum = "SUM";
    public const string Avg = "AVG";
    public const string Min = "MIN";
    public const string Max = "MAX";
    public const string Count = "COUNT";
}
