namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic SELECT examples demonstrating core query functionality.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class SelectExamples : ExampleBase
{
    public SelectExamples() : base(
        "SELECT - Basic Queries",
        "Demonstrates simple SELECT operations with WHERE, ORDER BY, pagination, and DISTINCT")
    {
    }

    /// <summary>
    /// Define all SELECT scenarios declaratively.
    /// Each scenario is a reusable data object with builder function.
    /// </summary>
    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple SELECT *",
            "Basic SELECT query retrieving all columns from a table",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with WHERE Clause",
            "SELECT with WHERE condition to filter rows by username",
            dialect => SqlBuilder.Select("UserId", "Username", "Email")
                .From("Users")
                .Where("Username", "john_doe")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with ORDER BY",
            "SELECT with ORDER BY DESC to sort results by price",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price")
                .From("Products")
                .OrderBy("Price DESC")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with Pagination (LIMIT/TOP/OFFSET)",
            "SELECT with pagination: SKIP(0).TAKE(10) - dialect-specific syntax",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(0)
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Take(10).Skip(5) - Method order independence",
            "Demonstrates that Take() and Skip() can be called in any order",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Take(10)           // Take first, then Skip - order doesn't matter!
                .Skip(5)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Skip(5) - Skip without Take",
            "Demonstrates Skip() works independently without Take()",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(5)            // Skip alone works independently
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
