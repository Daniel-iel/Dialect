namespace Dialect.Samples._03_Advanced;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Performance Optimization examples demonstrating efficient query patterns.
/// </summary>
public class PerformanceOptimizationExamples : ExampleBase
{
    public PerformanceOptimizationExamples() : base(
        "Performance Optimization Techniques",
        "Demonstrates efficient query patterns and selective data retrieval")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Selective Columns - Limited Result Set",
            "SELECT only needed columns instead of *",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("Email ASC")
                .Take(50)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Efficient Pagination - Skip and Take",
            "Pagination pattern with SKIP/TAKE",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Users")
                .OrderBy("Id DESC")
                .Skip(0)
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Optimized Filtering - Indexed Column",
            "Query on indexed column for fast lookup",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Where("Email", "john@example.com")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Range Query - Date Filtering",
            "Efficient date range query pattern",
            dialect => SqlBuilder.Select("Id", "Name", "CreatedDate")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("CreatedDate DESC")
                .Take(100)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Aggregation - Grouped Results",
            "Query with selective columns and ordering",
            dialect => SqlBuilder.Select("Department", "Count")
                .From("Employees")
                .OrderBy("Count DESC")
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Filtered Selection - Early WHERE",
            "Filtering tables before processing",
            dialect => SqlBuilder.Select("Id", "Name")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("Name ASC")
                .Take(50)
                .Build()
                .Compile(dialect)
        )
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


