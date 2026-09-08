namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic SELECT examples demonstrating core query functionality.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class SelectExamples : ExampleBase
{
    public SelectExamples() : base(
        "SELECT - Basic Queries",
        "Demonstrates simple SELECT operations with WHERE, ORDER BY, pagination, and DISTINCT")
    {
    }

    /// <summary>
    /// Define all SELECT scenarios declaratively.
    /// Each scenario is a reusable data object with builder function.
    /// </summary>
    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Simple SELECT *",
            "Basic SELECT query retrieving all columns from a table",
            dialect => SqlBuilder.Select("*")
                .From("Users")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with WHERE Clause",
            "SELECT with WHERE condition to filter rows by username",
            dialect => SqlBuilder.Select("UserId", "Username", "Email")
                .From("Users")
                .Where("Username", "john_doe")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with ORDER BY",
            "SELECT with ORDER BY DESC to sort results by price",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price")
                .From("Products")
                .OrderBy("Price DESC")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "SELECT with Pagination (LIMIT/TOP/OFFSET)",
            "SELECT with pagination: SKIP(0).TAKE(10) - dialect-specific syntax",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(0)
                .Take(10)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Take(10).Skip(5) - Method order independence",
            "Demonstrates that Take() and Skip() can be called in any order",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Take(10)           // Take first, then Skip - order doesn't matter!
                .Skip(5)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Skip(5) - Skip without Take",
            "Demonstrates Skip() works independently without Take()",
            dialect => SqlBuilder.Select("*")
                .From("Orders")
                .OrderBy("OrderId DESC")
                .Skip(5)            // Skip alone works independently
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - Comparison Operators
        new ScenarioDefinition(
            "WhereExpression - Greater Than (>)",
            "Using WhereExpression with GreaterThan comparison operator",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price")
                .From("Products")
                .Where("Price", ComparisonOperator.GreaterThan, 100m)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - Less Than or Equal (<=)",
            "Using WhereExpression with LessThanOrEqual comparison operator",
            dialect => SqlBuilder.Select("UserId", "Username", "CreatedDate")
                .From("Users")
                .Where("CreatedDate", ComparisonOperator.LessThanOrEqual, new DateTime(2024, 1, 1))
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - NOT Equal (<>)",
            "Using WhereExpression with NotEqual comparison operator",
            dialect => SqlBuilder.Select("OrderId", "Status", "Amount")
                .From("Orders")
                .Where("Status", ComparisonOperator.NotEqual, "Cancelled")
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - Complex Conditions (AND/OR)
        new ScenarioDefinition(
            "WhereExpression - AND Condition",
            "Combining two conditions with AND using ComparisonNode",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price", "StockQty")
                .From("Products")
                .Where(new AndNode(
                    new ComparisonNode(new Column("Price"), ComparisonOperator.GreaterThan, 50m),
                    new ComparisonNode(new Column("StockQty"), ComparisonOperator.GreaterThan, 0)
                ))
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - OR Condition",
            "Combining two conditions with OR using OrNode",
            dialect => SqlBuilder.Select("OrderId", "Status", "Amount", "UserId")
                .From("Orders")
                .Where(new OrNode(
                    new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Pending"),
                    new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Processing")
                ))
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - IN Clauses
        new ScenarioDefinition(
            "WhereExpression - IN Clause",
            "Using InNode to filter rows where column value is in a list",
            dialect => SqlBuilder.Select("OrderId", "Status", "UserId")
                .From("Orders")
                .Where(new InNode(new Column("Status"), new object?[] { "Pending", "Processing", "Shipped" }))
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - NOT IN Clause",
            "Using InNode with Negated=true to exclude values",
            dialect => SqlBuilder.Select("UserId", "Username", "Role")
                .From("Users")
                .Where(new InNode(new Column("Role"), new object?[] { "Admin", "SuperAdmin" }, Negated: true))
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - Nested Conditions
        new ScenarioDefinition(
            "WhereExpression - Nested AND/OR (Price > 100 AND (Status = 'Active' OR Status = 'Premium'))",
            "Complex nested conditions combining AND and OR operators",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price", "Status")
                .From("Products")
                .Where(new AndNode(
                    new ComparisonNode(new Column("Price"), ComparisonOperator.GreaterThan, 100m),
                    new OrNode(
                        new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Active"),
                        new ComparisonNode(new Column("Status"), ComparisonOperator.Equal, "Premium")
                    )
                ))
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - LIKE Pattern
        new ScenarioDefinition(
            "WhereExpression - LIKE Pattern",
            "Using LIKE operator for pattern matching in text columns",
            dialect => SqlBuilder.Select("UserId", "Username", "Email")
                .From("Users")
                .Where("Username", ComparisonOperator.Like, "%john%")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - NOT LIKE Pattern",
            "Using NOT LIKE operator to exclude pattern matches",
            dialect => SqlBuilder.Select("UserId", "Email", "Domain")
                .From("Users")
                .Where("Email", ComparisonOperator.NotLike, "%@spam.com")
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - NULL Checks
        new ScenarioDefinition(
            "WhereExpression - IS NULL",
            "Using IsNull operator to find rows with NULL values",
            dialect => SqlBuilder.Select("OrderId", "UserId", "Notes")
                .From("Orders")
                .Where("Notes", ComparisonOperator.IsNull, null)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - IS NOT NULL",
            "Using IsNotNull operator to find rows without NULL values",
            dialect => SqlBuilder.Select("UserId", "Username", "PhoneNumber")
                .From("Users")
                .Where("PhoneNumber", ComparisonOperator.IsNotNull, null)
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - BETWEEN
        new ScenarioDefinition(
            "WhereExpression - BETWEEN (Date Range)",
            "Using BETWEEN operator to filter by date range",
            dialect => SqlBuilder.Select("OrderId", "OrderDate", "Amount")
                .From("Orders")
                .Where("OrderDate", ComparisonOperator.Between, "2024-01-01,2024-12-31")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "WhereExpression - BETWEEN (Numeric Range)",
            "Using BETWEEN operator for price range filtering",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price")
                .From("Products")
                .Where("Price", ComparisonOperator.Between, "50,200")
                .Build()
                .Compile(dialect)
        ),

        // WhereExpression Examples - Complex Multi-Condition
        new ScenarioDefinition(
            "WhereExpression - Multi-Level AND/OR ((Price > 100 AND StockQty > 0) OR (Discount > 0 AND IsActive = true))",
            "Deeply nested conditions with multiple AND/OR branches",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price", "StockQty", "Discount", "IsActive")
                .From("Products")
                .Where(new OrNode(
                    new AndNode(
                        new ComparisonNode(new Column("Price"), ComparisonOperator.GreaterThan, 100m),
                        new ComparisonNode(new Column("StockQty"), ComparisonOperator.GreaterThan, 0)
                    ),
                    new AndNode(
                        new ComparisonNode(new Column("Discount"), ComparisonOperator.GreaterThan, 0m),
                        new ComparisonNode(new Column("IsActive"), ComparisonOperator.Equal, true)
                    )
                ))
                .Build()
                .Compile(dialect)
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
