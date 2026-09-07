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
        Console.WriteLine("\n");
        SelectWithFlexiblePagination();
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

        AddScenario("Simple SELECT *", results);
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

        AddScenario("SELECT with WHERE Clause", results);
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

        AddScenario("SELECT with ORDER BY", results);
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

        AddScenario("SELECT with Pagination (LIMIT/TOP/OFFSET)", results);
    }

    private void SelectWithFlexiblePagination()
    {
        OutputFormatter.PrintSubHeader("Example 5: SELECT with Flexible Pagination (Skip and Take in any order)");

        // Demonstrate that Skip() and Take() can be called in any order
        var orderIndependentResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Take(10)          // Take first
                .Skip(5)            // Then Skip - order doesn't matter!
                .Build()
                .Compile(dialect)
        );

        AddScenario("Take(10).Skip(5) - Same as Skip(5).Take(10)", orderIndependentResults);

        // Demonstrate Skip without Take
        var skipOnlyResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(5)            // Skip alone works independently
                .Build()
                .Compile(dialect)
        );

        AddScenario("Skip(5) - Skip without Take", skipOnlyResults);
    }
}
