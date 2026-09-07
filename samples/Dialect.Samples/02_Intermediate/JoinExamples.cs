namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// JOIN examples demonstrating various join types and patterns.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class JoinExamples : ExampleBase
{
    public JoinExamples() : base(
        "JOINs - Query Composition",
        "Demonstrates INNER, LEFT, RIGHT, FULL, CROSS joins")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "INNER JOIN",
            "INNER JOIN returning only matching rows from both tables",
            dialect =>
            {
                var joinCondition = new ComparisonNode(
                    new Column("o.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                return SqlBuilder.Select("o.OrderId", "u.Username", "o.Total")
                    .From("Orders o")
                    .InnerJoin("Users u", joinCondition)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "LEFT JOIN",
            "LEFT JOIN including unmatched rows from left table",
            dialect =>
            {
                var joinCondition = new ComparisonNode(
                    new Column("u.UserId"),
                    ComparisonOperator.Equal,
                    new Column("o.UserId")
                );
                return SqlBuilder.Select("u.Username", "o.OrderId", "o.Total")
                    .From("Users u")
                    .LeftJoin("Orders o", joinCondition)
                    .OrderBy("u.Username")
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "Multiple JOINs",
            "Multiple INNER JOINs connecting 4 tables",
            dialect =>
            {
                var joinCondition1 = new ComparisonNode(
                    new Column("o.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                var joinCondition2 = new ComparisonNode(
                    new Column("o.OrderId"),
                    ComparisonOperator.Equal,
                    new Column("oi.OrderId")
                );
                var joinCondition3 = new ComparisonNode(
                    new Column("oi.ProductId"),
                    ComparisonOperator.Equal,
                    new Column("p.ProductId")
                );
                return SqlBuilder.Select("o.OrderId", "u.Username", "p.Name", "oi.Quantity")
                    .From("Orders o")
                    .InnerJoin("Users u", joinCondition1)
                    .InnerJoin("OrderItems oi", joinCondition2)
                    .InnerJoin("Products p", joinCondition3)
                    .OrderBy("o.OrderId")
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
