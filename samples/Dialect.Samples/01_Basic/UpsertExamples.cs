namespace Dialect.Samples._01_Basic;

using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// UPSERT examples showing dialect-specific implementations.
/// </summary>
public class UpsertExamples : ExampleBase
{
    public UpsertExamples() : base(
        "UPSERT - Insert or Update",
        "Demonstrates SQL Server MERGE, PostgreSQL ON CONFLICT, MySQL ON DUPLICATE KEY UPDATE")
    {
    }

    public override void Run()
    {
        UpsertAll();
    }

    private void UpsertAll()
    {
        OutputFormatter.PrintSubHeader("UPSERT - Insert or Update");
        OutputFormatter.PrintWarning("Demonstrates SQL Server MERGE, PostgreSQL ON CONFLICT, MySQL ON DUPLICATE KEY UPDATE");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Upsert("Products")
                .Columns("ProductId", "Name", "Price")
                .Values(1, "Laptop Pro", 1299.99m)
                .OnConflict("ProductId")
                .UpdateSet("Name", "Laptop Pro")
                .UpdateSet("Price", 1299.99m)
                .Build()
                .Compile(dialect)
        );

        AddScenario("UPSERT - Insert or Update", results);
    }
}
