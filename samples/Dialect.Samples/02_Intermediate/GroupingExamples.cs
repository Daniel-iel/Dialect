namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Grouping and aggregation examples.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class GroupingExamples : ExampleBase
{
    public GroupingExamples() : base(
        "Grouping & Aggregation",
        "Demonstrates GROUP BY, HAVING, COUNT, SUM, AVG, MIN, MAX")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "GROUP BY and COUNT",
            "GROUP BY with COUNT aggregate function to count rows per group",
            dialect => SqlBuilder.Select("UserId", "COUNT(*) as OrderCount")
                .From("Orders")
                .GroupBy("UserId")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "GROUP BY with HAVING",
            "GROUP BY with HAVING clause filtering groups by aggregate condition",
            dialect =>
            {
                var havingCondition = new RawNode("SUM(Quantity) > 5");
                return SqlBuilder.Select("ProductId", "SUM(Quantity) as TotalQuantity")
                    .From("OrderItems")
                    .GroupBy("ProductId")
                    .Having(havingCondition)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "Multiple Aggregate Functions",
            "GROUP BY with COUNT, SUM, and AVG aggregate functions",
            dialect => SqlBuilder.Select("UserId", "COUNT(*) as TotalOrders", "SUM(Total) as TotalSpent", "AVG(Total) as AvgOrder")
                .From("Orders")
                .GroupBy("UserId")
                .OrderBy("TotalSpent DESC")
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
    }
}
