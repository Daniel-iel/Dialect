namespace Dialect.Samples._03_Advanced;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Database schema and query examples related to migrations.
/// </summary>
public class MigrationsExamples : ExampleBase
{
    public MigrationsExamples() : base(
        "Migrations & Schema Evolution",
        "Demonstrates versioned query patterns for schema management")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Initial Schema Query - Base Schema",
            "Query demonstrating basic schema structure",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Schema v2.0 - With IsActive Column",
            "Query using newly-added IsActive column",
            dialect => SqlBuilder.Select("Id", "Name", "Email", "IsActive")
                .From("Users")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Schema v2.1 - With Indexes",
            "Query optimized for indexed columns",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Where("Email", "john@example.com")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Filtering Query - Using New Columns",
            "Query leveraging newly-added columns",
            dialect => SqlBuilder.Select("Id", "Name", "Email", "IsActive")
                .From("Users")
                .Where("IsActive", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Backfill Pattern - UPDATE New Column",
            "Demonstrating backfill for new columns",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsActive", true)
                .Where("Id", true)
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


