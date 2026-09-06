namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic DELETE examples demonstrating deletion operations.
/// </summary>
public class DeleteExamples : ExampleBase
{
    public DeleteExamples() : base(
        "DELETE - Data Removal",
        "Demonstrates DELETE with WHERE clauses and safety features")
    {
    }

    public override void Run()
    {
        DeleteWithWhere();
        Console.WriteLine("\n");
        DeleteWithParameter();
        Console.WriteLine("\n");
        DeleteMultipleConditions();
    }

    private void DeleteWithWhere()
    {
        OutputFormatter.PrintSubHeader("Example 1: DELETE with Simple WHERE");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Delete()
                .From("Users")
                .Where("UserId", 5)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void DeleteWithParameter()
    {
        OutputFormatter.PrintSubHeader("Example 2: DELETE with Parameter");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Delete()
                .From("Orders")
                .Where("OrderId", "@orderId")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void DeleteMultipleConditions()
    {
        OutputFormatter.PrintSubHeader("Example 3: DELETE with Multiple Conditions");

        var condition1 = new ComparisonNode(new Column("OrderId"), ComparisonOperator.Equal, 1);
        var condition2 = new ComparisonNode(new Column("Quantity"), ComparisonOperator.GreaterThan, 0);
        var combined = new AndNode(condition1, condition2);

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Delete()
                .From("OrderItems")
                .Where(combined)
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
