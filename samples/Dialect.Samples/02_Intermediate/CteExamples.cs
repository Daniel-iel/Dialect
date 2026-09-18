namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// CTE (Common Table Expression) examples demonstrating WITH clauses.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class CteExamples : ExampleBase
{
    public CteExamples() : base(
        "CTEs - Common Table Expressions",
        "Demonstrates WITH clauses, single and multiple CTEs")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple CTE",
            "Single CTE with GROUP BY aggregating order counts per user",
            dialect =>
            {
                var cteQuery = SqlBuilder.Select("UserId", "COUNT(*) as OrderCount")
                    .From("Orders")
                    .GroupBy("UserId")
                    .Build();
                var joinCondition = new ComparisonNode(
                    new Column("uo.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                return SqlBuilder.Select("u.Username", "uo.OrderCount")
                    .With("UserOrders", cteQuery)
                    .From("UserOrders uo")
                    .InnerJoin("Users u", joinCondition)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "Multiple CTEs",
            "Multiple CTEs: first aggregates product sales, second filters top products",
            dialect =>
            {
                var productSalesCte = SqlBuilder.Select("ProductId", "SUM(Quantity) as TotalSold")
                    .From("OrderItems")
                    .GroupBy("ProductId")
                    .Build();
                var joinCondition2 = new ComparisonNode(
                    new Column("ps.ProductId"),
                    ComparisonOperator.Equal,
                    new Column("p.ProductId")
                );
                var topProductsCte = SqlBuilder.Select("p.ProductId", "p.Name", "ps.TotalSold")
                    .From("ProductSales ps")
                    .InnerJoin("Products p", joinCondition2)
                    .Where("ps.TotalSold", ComparisonOperator.GreaterThan, 5)
                    .Build();
                return SqlBuilder.Select("*")
                    .With("ProductSales", productSalesCte)
                    .With("TopProducts", topProductsCte)
                    .From("TopProducts")
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
