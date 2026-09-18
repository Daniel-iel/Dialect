namespace Dialect.Samples._04_ErrorHandling;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Error Handling examples demonstrating safe query patterns.
/// </summary>
public class ErrorHandlingExamples : ExampleBase
{
    public ErrorHandlingExamples() : base(
        "Error Handling & Safety Patterns",
        "Demonstrates safe query construction avoiding common errors")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Safe INSERT - Structured",
            "Using structured builder to prevent SQL injection",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Email", "Name", "IsActive")
                .Values("john@example.com", "John Doe", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Safe DELETE - With WHERE",
            "DELETE with explicit WHERE to prevent full-table deletes",
            dialect => SqlBuilder.Delete()
                .From("Users")
                .Where("Id", 1)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Safe UPDATE - Multiple Sets",
            "UPDATE with multiple columns and condition",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("Email", "newemail@example.com")
                .Set("IsActive", false)
                .Where("Id", 5)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Safe SELECT - Filtered and Limited",
            "SELECT with specific columns, WHERE clause, and LIMIT",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Users")
                .Where("IsActive", true)
                .OrderBy("Email ASC")
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


