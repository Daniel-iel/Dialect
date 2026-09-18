namespace Dialect.Samples._03_Advanced;
using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Core.Performance;
using Dialect.Samples.Utilities;

/// <summary>
/// Index advisor examples demonstrating index recommendations.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
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

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Index Advisor - Multi-column WHERE filtering",
            "Demonstrates index recommendations for multi-column WHERE clauses",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Where("Email", "user@example.com")
                .Where("Username", "john")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Index Advisor - JOIN and WHERE combination",
            "Demonstrates index recommendations for JOIN and WHERE patterns",
            dialect =>
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
            }
        ),

        new ScenarioDefinition(
            "Index Advisor - Covering index with ORDER BY",
            "Demonstrates covering index recommendations for SELECT with ORDER BY",
            dialect => SqlBuilder.Select("OrderId", "UserId", "Total")
                .From("Orders")
                .Where("UserId", 1)
                .OrderBy("OrderDate DESC")
                .Build()
                .Compile(dialect)
        ),
    };

    public override void Run()
    {
        foreach (var scenario in Scenarios)
        {
            var results = CompileForAllDialects(scenario.Builder);
            AddScenario(scenario.Name, results);
            Console.WriteLine("\n");
        }

        // Educational recommendations output
        Console.WriteLine("\n  Index Recommendations Summary:");
        Console.WriteLine("    1. WHERE clauses: Single and composite indexes on filtered columns");
        Console.WriteLine("    2. JOINs: Foreign key indexes on join columns");
        Console.WriteLine("    3. Covering indexes: Include SELECT columns for index-only scans");
    }
}
