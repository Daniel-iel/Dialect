namespace Dialect.Samples._03_Advanced;
using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Core.Performance;
using Dialect.Samples.Utilities;

/// <summary>
/// Index advisor examples demonstrating index recommendations.
/// 
/// NOTE: This is an educational example showing how the framework can identify index opportunities
/// for various query patterns (WHERE filtering, JOIN operations, and covering indexes).
/// 
/// The recommendations shown are illustrative and based on query structure analysis.
/// In production, index recommendations would be based on actual execution plans and 
/// workload analysis from real database instances.
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

    private void WhereColumnIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 1: Indexes for WHERE Clauses");

        // Build and compile a query for index analysis
        var compiledResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("*")
                .From("Users")
                .Where("Email", "user@example.com")
                .Where("Username", "john")
                .Build()
                .Compile(dialect)
        );

        AddScenario("Index Advisor - Multi-column WHERE filtering", compiledResults);

        const string sql = "SELECT * FROM Users WHERE Email = 'user@example.com' AND Username = 'john'";
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

        var whereIndexText = @$"
  Index Recommendations:
    1. Single-column index on Email (HIGH PRIORITY)
       - Estimated selectivity: 99.99%
       - Expected performance improvement: 100-1000x
    2. Composite index on (Email, Username) (MEDIUM PRIORITY)
       - Covers both filtering columns
       - Would enable index-only scans";

        Console.WriteLine(whereIndexText);
    }

    private void JoinColumnIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 2: Indexes for JOIN Columns");

        // Build and compile a query for JOIN index analysis
        var compiledResults = CompileForAllDialects(dialect =>
        {
            var joinCondition = new ComparisonNode(
                new Column("o.UserId"),
                ComparisonOperator.Equal,
                new Column("u.UserId")
            );
            
            return SqlBuilder.Select("OrderId", "UserId", "Total")
                .From("Orders o")
                .InnerJoin("Users u", joinCondition)
                .Build()
                .Compile(dialect);
        });


        AddScenario("Index Advisor - JOIN and WHERE combination", compiledResults);

        const string? joinIndexText = @$"
  Index Recommendations:
    1. Foreign key index on Orders.UserId (CRITICAL)
       - Used for JOIN operation
       - Prevents nested loop joins
    2. Index on Orders(UserId, Total) (HIGH PRIORITY)
       - Composite index covers both JOIN and WHERE
       - Enables index-only scans";

        Console.WriteLine(joinIndexText);
    }

    private void CompositeIndexes()
    {
        OutputFormatter.PrintSubHeader("Example 3: Covering Indexes");

        // Build and compile a query for composite index analysis
        var compiledResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("OrderId", "UserId", "Total")
                .From("Orders")
                .Where("UserId", 1)
                .OrderBy("OrderDate DESC")
                .Build()
                .Compile(dialect)
        );

        AddScenario("Index Advisor - Covering index with ORDER BY", compiledResults);

        const string? compositeIndexText = @$"
  Index Recommendation:
    Composite index on Orders(UserId, OrderDate, Total, OrderId)
       - Filters by UserId
       - Orders by OrderDate
       - Includes all selected columns (covering index)
       - Result: Complete index-only scan, zero table lookups";

        Console.WriteLine(compositeIndexText);
    }
}
