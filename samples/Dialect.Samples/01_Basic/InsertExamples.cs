namespace Dialect.Samples._01_Basic;

using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic INSERT examples demonstrating various insertion patterns.
/// </summary>
public class InsertExamples : ExampleBase
{
    public InsertExamples() : base(
        "INSERT - Data Insertion",
        "Demonstrates INSERT VALUES, multiple rows, and INSERT...SELECT patterns")
    {
    }

    public override void Run()
    {
        SimpleInsert();
        Console.WriteLine("\n");
        InsertMultipleRows();
        Console.WriteLine("\n");
        InsertWithParameters();
    }

    private void SimpleInsert()
    {
        OutputFormatter.PrintSubHeader("Example 1: Simple INSERT");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Insert()
                .Into("Users")
                .Columns("Username", "Email")
                .Values("new_user", "newuser@example.com")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void InsertMultipleRows()
    {
        OutputFormatter.PrintSubHeader("Example 2: INSERT Multiple Rows");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Insert()
                .Into("Products")
                .Columns("Name", "Price", "StockQuantity")
                .Values("Tablet", 599.99m, 30)
                .Values("USB Cable", 9.99m, 500)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void InsertWithParameters()
    {
        OutputFormatter.PrintSubHeader("Example 3: INSERT with Parameters");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Insert()
                .Into("Users")
                .Columns("Username", "Email")
                .Values("@username", "@email")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
