namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Window Function examples demonstrating analytical queries.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class WindowFunctionExamples : ExampleBase
{
    public WindowFunctionExamples() : base(
        "Window Functions - Analytical Queries",
        "Demonstrates ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Window Function - ROW_NUMBER",
            "ROW_NUMBER window function partitioned by UserId ordered by OrderDate",
            dialect =>
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
        ),

        new ScenarioDefinition(
            "Window Function - RANK and DENSE_RANK",
            "RANK window function ordered by Total descending",
            dialect =>
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
        ),

        new ScenarioDefinition(
            "Window Function - LAG and LEAD",
            "LAG window function to access previous row's Total value",
            dialect =>
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
        ),
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
