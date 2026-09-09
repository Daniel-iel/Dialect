namespace Dialect.Samples._04_ErrorHandling;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Query Debugging examples showing how to inspect compiled queries.
/// </summary>
public class DebuggingExamples : ExampleBase
{
    public DebuggingExamples() : base(
        "Query Debugging & Inspection",
        "Demonstrates query structure inspection and parameter visibility")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Query with Parameters - Email and Status",
            "SELECT with WHERE clause showing parameter binding",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Users")
                .Where("Email", "john@example.com")
                .OrderBy("Name ASC")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Complex Query - Multiple Conditions",
            "SELECT with multiple WHERE conditions",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("CreatedDate DESC")
                .Take(100)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Filtered Selection with Pagination",
            "Demonstrating parameter inspection with SKIP/TAKE",
            dialect => SqlBuilder.Select("Id", "Name", "Email", "CreatedDate")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("CreatedDate DESC")
                .Skip(0)
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Sorted Query with Column Selection",
            "SELECT showing explicit column selection and ordering",
            dialect => SqlBuilder.Select("Id", "Name", "Email", "LastLoginDate")
                .From("Users")
                .OrderBy("LastLoginDate DESC")
                .Take(50)
                .Build()
                .Compile(dialect)
        )
    };

    public override void Run()
    {
        Console.WriteLine("\n--- Query Debugging Examples ---\n");
        
        foreach (var scenario in Scenarios)
        {
            var results = CompileForAllDialects(scenario.Builder);
            AddScenario(scenario.Name, results);
            Console.WriteLine("\n");
        }
    }
}


