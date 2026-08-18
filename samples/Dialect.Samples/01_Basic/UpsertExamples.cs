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
        SqlServerMerge();
        Console.WriteLine("\n");
        PostgreSqlOnConflict();
        Console.WriteLine("\n");
        MySqlOnDuplicateKey();
    }

    private void SqlServerMerge()
    {
        OutputFormatter.PrintSubHeader("SQL Server: MERGE Statement");
        OutputFormatter.PrintWarning("Demonstrates SQL Server specific MERGE syntax");
        
        var dialect = DialectHelper.GetDialect("SQL Server");
        
        var compiled = SqlBuilder.Upsert("Products")
            .Columns("ProductId", "Name", "Price")
            .Values(1, "Laptop Pro", 1299.99m)
            .OnConflict("ProductId")
            .UpdateSet("Name", "Laptop Pro")
            .UpdateSet("Price", 1299.99m)
            .Build()
            .Compile(dialect);
        
        PrintResult("SQL Server", compiled.Sql, compiled.Parameters);
    }

    private void PostgreSqlOnConflict()
    {
        OutputFormatter.PrintSubHeader("PostgreSQL: ON CONFLICT DO UPDATE");
        OutputFormatter.PrintWarning("Demonstrates PostgreSQL specific ON CONFLICT syntax");
        
        var dialect = DialectHelper.GetDialect("PostgreSQL");
        
        var compiled = SqlBuilder.Upsert("Products")
            .Columns("ProductId", "Name", "Price")
            .Values(1, "Laptop Pro", 1299.99m)
            .OnConflict("product_id")
            .UpdateSet("name", "Laptop Pro")
            .UpdateSet("price", 1299.99m)
            .Build()
            .Compile(dialect);
        
        PrintResult("PostgreSQL", compiled.Sql, compiled.Parameters);
    }

    private void MySqlOnDuplicateKey()
    {
        OutputFormatter.PrintSubHeader("MySQL: ON DUPLICATE KEY UPDATE");
        OutputFormatter.PrintWarning("Demonstrates MySQL specific ON DUPLICATE KEY UPDATE syntax");
        
        var dialect = DialectHelper.GetDialect("MySQL");
        
        var compiled = SqlBuilder.Upsert("Products")
            .Columns("ProductId", "Name", "Price")
            .Values(1, "Laptop Pro", 1299.99m)
            .OnConflict("ProductId")
            .UpdateSet("Name", "Laptop Pro")
            .UpdateSet("Price", 1299.99m)
            .Build()
            .Compile(dialect);
        
        PrintResult("MySQL", compiled.Sql, compiled.Parameters);
    }
}
