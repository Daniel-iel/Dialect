namespace Dialect.Samples._01_Basic;

using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic INSERT examples demonstrating various insertion patterns.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class InsertExamples : ExampleBase
{
    public InsertExamples() : base(
        "INSERT - Data Insertion",
        "Demonstrates INSERT VALUES, multiple rows, and INSERT...SELECT patterns")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple INSERT",
            "Basic INSERT with single row of values",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Username", "Email")
                .Values("new_user", "newuser@example.com")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INSERT Multiple Rows",
            "INSERT statement with multiple rows of values",
            dialect => SqlBuilder.Insert()
                .Into("Products")
                .Columns("Name", "Price", "StockQuantity")
                .Values("Tablet", 599.99m, 30)
                .Values("USB Cable", 9.99m, 500)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INSERT with Parameters",
            "INSERT using parameterized values for SQL injection prevention",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Username", "Email")
                .Values("@username", "@email")
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
