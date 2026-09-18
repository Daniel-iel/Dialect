namespace Dialect.Samples._03_Advanced;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Query Analysis examples showing query patterns and best practices.
/// </summary>
public class QueryAnalysisExamples : ExampleBase
{
    public QueryAnalysisExamples() : base(
        "Query Analysis & Optimization Patterns",
        "Demonstrates query construction patterns for filtering, ordering, and pagination")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple WHERE - Single Predicate",
            "Basic filtering on indexed column",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Where("Email", "john@example.com")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Filtered SELECT - Multiple Columns",
            "SELECT with column selection and WHERE",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("Email ASC")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Ordered Query - Descending with Limit",
            "SELECT with ORDER BY DESC and LIMIT",
            dialect => SqlBuilder.Select("Id", "Name", "CreatedDate")
                .From("Users")
                .OrderBy("CreatedDate DESC")
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Paginated Query - Skip and Take",
            "SELECT with pagination using SKIP/TAKE",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderDate DESC")
                .Skip(10)
                .Take(20)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Selective Columns - Date Filtering",
            "SELECT specific columns with date condition",
            dialect => SqlBuilder.Select("Id", "Name", "CreatedDate", "LastLoginDate")
                .From("Users")
                .Where("CreatedDate", true)
                .OrderBy("LastLoginDate DESC")
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


