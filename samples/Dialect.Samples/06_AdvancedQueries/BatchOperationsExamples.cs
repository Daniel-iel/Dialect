namespace Dialect.Samples._06_AdvancedQueries;

using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Batch Operations examples demonstrating bulk data manipulation patterns.
/// </summary>
public class BatchOperationsExamples : ExampleBase
{
    public BatchOperationsExamples() : base(
        "Batch Operations - Bulk Data Manipulation",
        "Demonstrates INSERT, UPDATE, DELETE operations for data management")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "INSERT - Single Record",
            "Basic INSERT operation with column values",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Email", "Name", "IsActive")
                .Values("john@example.com", "John Doe", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INSERT - Multiple Records (Second)",
            "Demonstrates another INSERT pattern",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Email", "Name", "IsActive")
                .Values("jane@example.com", "Jane Smith", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE - Bulk with WHERE clause",
            "Update records matching condition",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsActive", false)
                .Where("Id", 1)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE - Multiple SET columns",
            "Update operation with multiple columns",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsActive", false)
                .Set("UpdatedDate", "GETDATE()")
                .Where("CreatedDate", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "DELETE - Safe with WHERE",
            "Delete records matching criteria safely",
            dialect => SqlBuilder.Delete()
                .From("Users")
                .Where("Id", 123)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Soft Delete Pattern - UPDATE IsDeleted",
            "Using UPDATE to mark records as deleted",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsDeleted", true)
                .Where("Id", 456)
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


