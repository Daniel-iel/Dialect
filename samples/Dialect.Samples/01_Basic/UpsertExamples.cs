namespace Dialect.Samples._01_Basic;

using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// UPSERT examples showing dialect-specific implementations.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class UpsertExamples : ExampleBase
{
    public UpsertExamples() : base(
        "UPSERT - Insert or Update",
        "Demonstrates SQL Server MERGE, PostgreSQL ON CONFLICT, MySQL ON DUPLICATE KEY UPDATE")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "UPSERT - Insert or Update",
            "Dialect-specific UPSERT: SQL Server MERGE, PostgreSQL ON CONFLICT, MySQL ON DUPLICATE KEY UPDATE",
            dialect => SqlBuilder.Upsert("Products")
                .Columns("ProductId", "Name", "Price")
                .Values(1, "Laptop Pro", 1299.99m)
                .OnConflict("ProductId")
                .UpdateSet("Name", "Laptop Pro")
                .UpdateSet("Price", 1299.99m)
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
