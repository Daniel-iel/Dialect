namespace Dialect.Samples._02_Intermediate;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Demonstrates subquery patterns: FROM (derived tables) and WHERE IN (subqueries).
/// Subqueries enable complex queries with aggregations, filtering, and set operations.
/// </summary>
public class SubqueryExamples : ExampleBase
{
    public SubqueryExamples() : base(
        "Subquery Patterns",
        "Demonstrates FROM (Derived Tables) and WHERE IN Subqueries")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        // ============ FROM SUBQUERIES (Derived Tables) ============
        new ScenarioDefinition(
            "FROM Subquery - Basic Derived Table",
            "Using a subquery in FROM clause as a derived table",
            dialect =>
            {
                var subquery = SqlBuilder.Select("UserId", "COUNT(*) as OrderCount")
                    .From("Orders")
                    .GroupBy("UserId")
                    .Build();

                return SqlBuilder.Select("UserId", "OrderCount")
                    .From(subquery, "OrderSummary")
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "FROM Subquery - Filtered Derived Table",
            "Subquery with WHERE clause, then filter results further",
            dialect =>
            {
                var subquery = SqlBuilder.Select("ProductId", "SUM(Quantity) as TotalSold")
                    .From("OrderItems")
                    .GroupBy("ProductId")
                    .Build();

                return SqlBuilder.Select("ProductId", "TotalSold")
                    .From(subquery, "HighValueProducts")
                    .Where(new ComparisonNode(new Column("TotalSold"), ComparisonOperator.GreaterThanOrEqual, 100))
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "FROM Subquery - Nested Aggregations",
            "Subquery aggregates, then outer query processes aggregates",
            dialect =>
            {
                var subquery = SqlBuilder.Select("CategoryId", "AVG(Price) as AvgPrice")
                    .From("Products")
                    .GroupBy("CategoryId")
                    .Build();

                return SqlBuilder.Select("CategoryId", "AvgPrice")
                    .From(subquery, "CategoryStats")
                    .OrderBy("AvgPrice DESC")
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "FROM Subquery - Complex Aggregation",
            "Multiple aggregations in subquery with HAVING clause",
            dialect =>
            {
                var subquery = SqlBuilder.Select("UserId", "COUNT(*) as OrderCount", "SUM(TotalAmount) as TotalSpent")
                    .From("Orders")
                    .GroupBy("UserId")
                    .Having(new ComparisonNode(new Column("OrderCount"), ComparisonOperator.GreaterThan, 5))
                    .Build();

                return SqlBuilder.Select("UserId", "OrderCount", "TotalSpent")
                    .From(subquery, "ActiveUserStats")
                    .Where(new ComparisonNode(new Column("TotalSpent"), ComparisonOperator.GreaterThan, 1000))
                    .Build()
                    .Compile(dialect);
            }
        ),

        // ============ WHERE IN SUBQUERIES ============
        new ScenarioDefinition(
            "WHERE IN Subquery - Basic",
            "Find rows where column value matches any value from subquery",
            dialect =>
            {
                var subquery = SqlBuilder.Select("UserId")
                    .From("Users")
                    .Where(new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Active"))
                    .Build();

                return SqlBuilder.Select("OrderId", "UserId", "OrderDate", "TotalAmount")
                    .From("Orders")
                    .WhereIn("UserId", subquery)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE NOT IN Subquery - Exclusion Pattern",
            "Find rows where column value does NOT match subquery results",
            dialect =>
            {
                var subquery = SqlBuilder.Select("ProductId")
                    .From("DiscontinuedProducts")
                    .Build();

                return SqlBuilder.Select("ProductId", "Name", "Price")
                    .From("Products")
                    .WhereNotIn("ProductId", subquery)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE IN Subquery - With Aggregation",
            "Subquery uses HAVING to find matching entities",
            dialect =>
            {
                var subquery = SqlBuilder.Select("ProductId")
                    .From("OrderItems")
                    .GroupBy("ProductId")
                    .Having(new ComparisonNode(new Column("ProductCount"), ComparisonOperator.GreaterThan, 10))
                    .Build();

                return SqlBuilder.Select("ProductId", "Name", "Price")
                    .From("Products")
                    .WhereIn("ProductId", subquery)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE IN Subquery - Chained WITH Conditions",
            "Combine subquery with other WHERE conditions",
            dialect =>
            {
                var subquery = SqlBuilder.Select("CategoryId")
                    .From("Categories")
                    .Where(new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Featured"))
                    .Build();

                return SqlBuilder.Select("ProductId", "Name", "Price", "CategoryId")
                    .From("Products")
                    .Where(new AndNode(
                        new InNode(new Column("CategoryId"), null, subquery),
                        new ComparisonNode(new Column("Price"), ComparisonOperator.GreaterThan, 100)
                    ))
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE IN Subquery - Multiple Conditions",
            "WHERE IN with AND/OR logic in both outer and subquery",
            dialect =>
            {
                var subquery = SqlBuilder.Select("UserId")
                    .From("Users")
                    .Where(new AndNode(
                        new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Active"),
                        new ComparisonNode(new Column("CreatedAt"), ComparisonOperator.GreaterThan, "2023-01-01")
                    ))
                    .Build();

                return SqlBuilder.Select("OrderId", "UserId", "OrderDate", "TotalAmount")
                    .From("Orders")
                    .Where(new AndNode(
                        new InNode(new Column("UserId"), null, subquery),
                        new ComparisonNode(new Column("TotalAmount"), ComparisonOperator.GreaterThanOrEqual, 50)
                    ))
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE IN Subquery - Using SelectBuilder",
            "Pass SelectBuilder directly without calling Build()",
            dialect =>
            {
                var subquery = SqlBuilder.Select("CustomerId")
                    .From("CustomerPreferences")
                    .Where(new ComparisonNode(new Column("Preference"), ComparisonOperator.Equal, "Newsletter"));

                return SqlBuilder.Select("OrderId", "CustomerId", "OrderDate")
                    .From("Orders")
                    .WhereIn("CustomerId", subquery)
                    .Build()
                    .Compile(dialect);
            }
        ),

        new ScenarioDefinition(
            "WHERE NOT IN Subquery - Combining OR Logic",
            "NOT IN with OR condition to exclude multiple categories",
            dialect =>
            {
                var subquery = SqlBuilder.Select("ProductId")
                    .From("Products")
                    .Where(new OrNode(
                        new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Discontinued"),
                        new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "OnHold")
                    ))
                    .Build();

                return SqlBuilder.Select("ProductId", "Name", "Price", "Status")
                    .From("Products")
                    .WhereNotIn("ProductId", subquery)
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
