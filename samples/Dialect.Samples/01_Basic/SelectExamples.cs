namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic SELECT examples demonstrating core query functionality.
/// </summary>
public class SelectExamples : ExampleBase
{
    public SelectExamples() : base(
        "SELECT - Basic Queries",
        "Demonstrates simple SELECT operations with WHERE, ORDER BY, pagination, and DISTINCT")
    {
    }

    public override void Run()
    {
        SimpleSelect();
        Console.WriteLine("\n");
        SelectWithWhere();
        Console.WriteLine("\n");
        SelectWithOrderBy();
        Console.WriteLine("\n");
        SelectWithPagination();
    }

    private void SimpleSelect()
    {
        OutputFormatter.PrintSubHeader("Example 1: Simple SELECT *");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("*")
                .From("Users")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void SelectWithWhere()
    {
        OutputFormatter.PrintSubHeader("Example 2: SELECT with WHERE Clause");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("UserId", "Username", "Email")
                .From("Users")
                .Where("Username", "john_doe")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void SelectWithOrderBy()
    {
        OutputFormatter.PrintSubHeader("Example 3: SELECT with ORDER BY");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("ProductId", "Name", "Price")
                .From("Products")
                .OrderBy("Price DESC")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void SelectWithPagination()
    {
        OutputFormatter.PrintSubHeader("Example 4: SELECT with Pagination (LIMIT/TOP/OFFSET)");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(0)
                .Take(10)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
