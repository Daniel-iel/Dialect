namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic UPDATE examples demonstrating update operations.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class UpdateExamples : ExampleBase
{
    public UpdateExamples() : base(
        "UPDATE - Data Modification",
        "Demonstrates UPDATE SET, WHERE clauses, and safety features")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple UPDATE",
            "Basic UPDATE with single column and WHERE condition",
            dialect => SqlBuilder.Update()
                .Table("Products")
                .Set("StockQuantity", 100)
                .Where("ProductId", 1)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE with WHERE",
            "UPDATE using parameterized WHERE condition",
            dialect => SqlBuilder.Update()
                .Table("Orders")
                .Set("Total", 1500.00m)
                .Where("OrderId", "@orderId")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE Multiple Columns",
            "UPDATE statement modifying multiple columns with WHERE",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("Username", "updated_name")
                .Set("Email", "updated@example.com")
                .Where("UserId", 1)
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
