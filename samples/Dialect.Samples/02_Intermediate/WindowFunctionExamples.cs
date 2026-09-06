namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Window Function examples demonstrating analytical queries.
/// </summary>
public class WindowFunctionExamples : ExampleBase
{
    public WindowFunctionExamples() : base(
        "Window Functions - Analytical Queries",
        "Demonstrates ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD")
    {
    }

    public override void Run()
    {
        RowNumber();
        Console.WriteLine("\n");
        RankAndDenseRank();
        Console.WriteLine("\n");
        LagAndLead();
    }

    private void RowNumber()
    {
        OutputFormatter.PrintSubHeader("Example 1: ROW_NUMBER");

        var results = CompileForAllDialects(dialect =>
        {
            var orderByItems = new List<OrderByClause>
            {
                new OrderByClause(new Column("OrderDate"), SortDirection.Ascending)
            };

            return SqlBuilder.Select("OrderId", "UserId", "Total")
                .From("Orders")
                .SelectWindow(
                    functionName: "ROW_NUMBER",
                    partitionByColumns: new[] { "UserId" },
                    orderByItems: orderByItems,
                    alias: "RowNum"
                )
                .Build()
                .Compile(dialect);
        }
        );

        PrintResults(results);
    }

    private void RankAndDenseRank()
    {
        OutputFormatter.PrintSubHeader("Example 2: RANK and DENSE_RANK");

        var results = CompileForAllDialects(dialect =>
        {
            var orderByItems = new List<OrderByClause>
            {
                new OrderByClause(new Column("Total"), SortDirection.Descending)
            };

            return SqlBuilder.Select("UserId", "Total")
                .From("Orders")
                .SelectWindow(
                    functionName: "RANK",
                    partitionByColumns: null,
                    orderByItems: orderByItems,
                    alias: "Rank"
                )
                .Build()
                .Compile(dialect);
        }
        );

        PrintResults(results);
    }

    private void LagAndLead()
    {
        OutputFormatter.PrintSubHeader("Example 3: LAG and LEAD");

        var results = CompileForAllDialects(dialect =>
        {
            var orderByItems = new List<OrderByClause>
            {
                new OrderByClause(new Column("OrderDate"), SortDirection.Ascending)
            };

            return SqlBuilder.Select("OrderId", "Total")
                .From("Orders")
                .SelectWindowAnalytical(
                    functionName: "LAG",
                    columnArg: "Total",
                    partitionByColumns: null,
                    orderByItems: orderByItems,
                    alias: "PreviousTotal"
                )
                .Build()
                .Compile(dialect);
        }
        );

        PrintResults(results);
    }
}
