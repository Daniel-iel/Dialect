namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Grouping and aggregation examples.
/// </summary>
public class GroupingExamples : ExampleBase
{
    public GroupingExamples() : base(
        "Grouping & Aggregation",
        "Demonstrates GROUP BY, HAVING, COUNT, SUM, AVG, MIN, MAX")
    {
    }

    public override void Run()
    {
        GroupByAndCount();
        Console.WriteLine("\n");
        GroupByWithHaving();
        Console.WriteLine("\n");
        MultipleAggregates();
    }

    private void GroupByAndCount()
    {
        OutputFormatter.PrintSubHeader("Example 1: GROUP BY with COUNT");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("UserId", "COUNT(*) as OrderCount")
                .From("Orders")
                .GroupBy("UserId")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }

    private void GroupByWithHaving()
    {
        OutputFormatter.PrintSubHeader("Example 2: GROUP BY with HAVING");

        var results = CompileForAllDialects(dialect =>
        {
            var havingCondition = new RawNode("SUM(Quantity) > 5");
            return SqlBuilder.Select("ProductId", "SUM(Quantity) as TotalQuantity")
                .From("OrderItems")
                .GroupBy("ProductId")
                .Having(havingCondition)
                .Build()
                .Compile(dialect);
        }
        );

        PrintResults(results);
    }

    private void MultipleAggregates()
    {
        OutputFormatter.PrintSubHeader("Example 3: Multiple Aggregate Functions");

        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select("UserId", "COUNT(*) as TotalOrders", "SUM(Total) as TotalSpent", "AVG(Total) as AvgOrder")
                .From("Orders")
                .GroupBy("UserId")
                .OrderBy("TotalSpent DESC")
                .Build()
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
