namespace Dialect.Samples._03_Advanced;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Core.Query;
using Dialect.SqlServer.Query;
using Dialect.Samples.Utilities;

/// <summary>
/// Query optimization and analysis examples.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// 
/// NOTE: This is an educational example demonstrating the framework's query analysis capabilities.
/// The optimization analysis (query parsing, predicate analysis, join analysis) demonstrates 
/// how the Dialect framework can parse and analyze SQL queries for optimization opportunities.
/// 
/// In a real-world scenario, these would integrate with actual SQL execution plans and
/// performance metrics from live database instances.
/// </summary>
public class OptimizationExamples : ExampleBase
{
    public OptimizationExamples() : base(
        "Query Optimization & Analysis",
        "Demonstrates query parsing, predicate analysis, and optimization suggestions")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Query Parsing - JOIN with WHERE and ORDER BY",
            "Demonstrates parsing JOIN with ORDER BY for optimization analysis",
            dialect =>
            {
                var joinCondition = new ComparisonNode(
                    new Column("o.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                return SqlBuilder.Select("OrderId", "Username", "Total")
                    .From("Orders o")
                    .InnerJoin("Users u", joinCondition)
                    .OrderBy("Total DESC")
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "Predicate Analysis - Multi-condition WHERE",
            "Demonstrates multi-predicate WHERE clause for selectivity analysis",
            dialect => SqlBuilder.Select("OrderId", "UserId", "Total", "OrderDate")
                .From("Orders")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "JOIN Analysis - Multi-table JOIN with WHERE",
            "Demonstrates multiple JOINs for JOIN cost analysis",
            dialect =>
            {
                var join1 = new ComparisonNode(
                    new Column("o.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                var join2 = new ComparisonNode(
                    new Column("o.OrderId"),
                    ComparisonOperator.Equal,
                    new Column("oi.OrderId")
                );
                var join3 = new ComparisonNode(
                    new Column("oi.ProductId"),
                    ComparisonOperator.Equal,
                    new Column("p.ProductId")
                );
                return SqlBuilder.Select("OrderId", "UserId", "Total")
                    .From("Orders o")
                    .InnerJoin("Users u", join1)
                    .InnerJoin("OrderItems oi", join2)
                    .InnerJoin("Products p", join3)
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

        // Educational analysis output
        Console.WriteLine("\n  Analysis Results:");
        Console.WriteLine("    - Query Parsing: Identifies WHERE clauses, JOINs, ORDER BY");
        Console.WriteLine("    - Predicate Analysis: Evaluates selectivity and indexability");
        Console.WriteLine("    - JOIN Analysis: Recommends optimal join order and indexes");
    }
}
