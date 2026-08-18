namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic UPDATE examples demonstrating update operations.
/// </summary>
public class UpdateExamples : ExampleBase
{
    public UpdateExamples() : base(
        "UPDATE - Data Modification",
        "Demonstrates UPDATE SET, WHERE clauses, and safety features")
    {
    }

    public override void Run()
    {
        SimpleUpdate();
        Console.WriteLine("\n");
        UpdateWithWhere();
        Console.WriteLine("\n");
        UpdateMultipleColumns();
    }

    private void SimpleUpdate()
    {
        OutputFormatter.PrintSubHeader("Example 1: Simple UPDATE");
        
        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Update()
                .Table("Products")
                .Set("StockQuantity", 100)
                .Where("ProductId", 1)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void UpdateWithWhere()
    {
        OutputFormatter.PrintSubHeader("Example 2: UPDATE with WHERE");
        
        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Update()
                .Table("Orders")
                .Set("Total", 1500.00m)
                .Where("OrderId", "@orderId")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void UpdateMultipleColumns()
    {
        OutputFormatter.PrintSubHeader("Example 3: UPDATE Multiple Columns");
        
        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Update()
                .Table("Users")
                .Set("Username", "updated_name")
                .Set("Email", "updated@example.com")
                .Where("UserId", 1)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
