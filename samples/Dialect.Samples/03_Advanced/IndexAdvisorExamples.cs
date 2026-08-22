namespace Dialect.Samples._03_Advanced;

using Dialect.Core.Dialects;
using Dialect.Core.Indexing;
using Dialect.Core.Performance;
using Dialect.Samples.Utilities;

/// <summary>
/// Index advisor examples demonstrating index recommendations.
/// </summary>
public class IndexAdvisorExamples : ExampleBase
{
    public IndexAdvisorExamples() : base(
        "Index Advisor - Performance Tuning",
        "Demonstrates index recommendations for various query patterns")
    {
    }

    public override void Run()
    {
        WhereColumnIndexes();
        Console.WriteLine("\n");
        JoinColumnIndexes();
        Console.WriteLine("\n");
        CompositeIndexes();
    }

    private static void WhereColumnIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 1: Indexes for WHERE Clauses");
        
        var sql = "SELECT * FROM Users WHERE Email = 'user@example.com' AND Username = 'john'";
        var metrics = new PerformanceMetrics(
            TotalCost: 100.5m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 1,
            TotalRowsExamined: 10000,
            TotalRowsProduced: 1,
            Selectivity: 0.0001,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 45.2,
            MissingIndexRecommendations: new[] { "Index on Email", "Composite index on (Email, Username)" },
            OptimizationTips: new[] { "Add index on frequently filtered columns", "Consider composite index for multi-column predicates" }
        );
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"  Performance Metrics: Rows Scanned={metrics.TotalRowsExamined}, Returned={metrics.TotalRowsProduced}, Cost={metrics.TotalCost}");
        Console.WriteLine($"\n  Index Recommendations:");
        Console.WriteLine($"    1. Single-column index on Email (HIGH PRIORITY)");
        Console.WriteLine($"       - Estimated selectivity: 99.99%");
        Console.WriteLine($"       - Expected performance improvement: 100-1000x");
        Console.WriteLine($"    2. Composite index on (Email, Username) (MEDIUM PRIORITY)");
        Console.WriteLine($"       - Covers both filtering columns");
        Console.WriteLine($"       - Would enable index-only scans");
    }

    private static void JoinColumnIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 2: Indexes for JOIN Columns");
        
        var sql = "SELECT * FROM Orders o JOIN Users u ON o.UserId = u.UserId WHERE o.Total > 500";
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"\n  Index Recommendations:");
        Console.WriteLine($"    1. Foreign key index on Orders.UserId (CRITICAL)");
        Console.WriteLine($"       - Used for JOIN operation");
        Console.WriteLine($"       - Prevents nested loop joins");
        Console.WriteLine($"    2. Index on Orders(UserId, Total) (HIGH PRIORITY)");
        Console.WriteLine($"       - Composite index covers both JOIN and WHERE");
        Console.WriteLine($"       - Enables index-only scans");
    }

    private static void CompositeIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 3: Covering Indexes");
        
        var sql = "SELECT OrderId, UserId, Total FROM Orders WHERE UserId = 1 ORDER BY OrderDate";
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"\n  Index Recommendation:");
        Console.WriteLine($"    Composite index on Orders(UserId, OrderDate, Total, OrderId)");
        Console.WriteLine($"       - Filters by UserId");
        Console.WriteLine($"       - Orders by OrderDate");
        Console.WriteLine($"       - Includes all selected columns (covering index)");
        Console.WriteLine($"       - Result: Complete index-only scan, zero table lookups");
    }
}
