namespace Dialect.Samples._06_AdvancedQueries;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Batch Operations examples demonstrating bulk data manipulation patterns.
/// </summary>
public class BatchOperationsExamples : ExampleBase
{
    public BatchOperationsExamples() : base(
        "Batch Operations - Bulk Data Manipulation",
        "Demonstrates INSERT, UPDATE, DELETE operations for data management")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "INSERT - Single Record",
            "Basic INSERT operation with column values",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Email", "Name", "IsActive")
                .Values("john@example.com", "John Doe", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INSERT - Multiple Records (Second)",
            "Demonstrates another INSERT pattern",
            dialect => SqlBuilder.Insert()
                .Into("Users")
                .Columns("Email", "Name", "IsActive")
                .Values("jane@example.com", "Jane Smith", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE - Bulk with WHERE clause",
            "Update records matching condition",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsActive", false)
                .Where("Id", 1)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UPDATE - Multiple SET columns",
            "Update operation with multiple columns",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsActive", false)
                .Set("UpdatedDate", "GETDATE()")
                .Where("CreatedDate", true)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "DELETE - Safe with WHERE",
            "Delete records matching criteria safely",
            dialect => SqlBuilder.Delete()
                .From("Users")
                .Where("Id", 123)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Soft Delete Pattern - UPDATE IsDeleted",
            "Using UPDATE to mark records as deleted",
            dialect => SqlBuilder.Update()
                .Table("Users")
                .Set("IsDeleted", true)
                .Where("Id", 456)
                .Build()
                .Compile(dialect)
        ),

        // UNION Examples - Set Operations
        new ScenarioDefinition(
            "UNION - Combine two result sets (removes duplicates)",
            "UNION combines results from two SELECT statements, removing duplicate rows",
            dialect => SqlBuilder.Select("Id", "Name", "Email")
                .From("Employees")
                .Where("Status", "Active")
                .Union(
                    SqlBuilder.Select("Id", "Name", "Email")
                        .From("Contractors")
                        .Where("Status", "Active")
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UNION ALL - Combine result sets with duplicates",
            "UNION ALL combines all rows including duplicates (faster than UNION)",
            dialect => SqlBuilder.Select("ProductId", "ProductName", "Price")
                .From("CurrentInventory")
                .UnionAll(
                    SqlBuilder.Select("ProductId", "ProductName", "Price")
                        .From("ArchiveInventory")
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INTERSECT - Find common rows in two result sets",
            "INTERSECT returns only rows that appear in both SELECT statements",
            dialect => SqlBuilder.Select("UserId", "Email")
                .From("ActiveUsers")
                .Intersect(
                    SqlBuilder.Select("UserId", "Email")
                        .From("PremiumSubscribers")
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "EXCEPT - Rows in first set but not in second",
            "EXCEPT returns rows from the left SELECT that don't appear in the right SELECT",
            dialect => SqlBuilder.Select("UserId", "Email")
                .From("AllUsers")
                .Except(
                    SqlBuilder.Select("UserId", "Email")
                        .From("BannedUsers")
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UNION with WHERE clauses",
            "Combining multiple filtered queries with UNION",
            dialect => SqlBuilder.Select("Id", "Name", "Department", "Salary")
                .From("Employees")
                .Where("Salary", ComparisonOperator.GreaterThan, 75000m)
                .Union(
                    SqlBuilder.Select("Id", "Name", "Department", "Salary")
                        .From("Contractors")
                        .Where("HourlyRate", ComparisonOperator.GreaterThan, 150m)
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "UNION ALL with pagination",
            "UNION ALL combining multiple result sets",
            dialect => SqlBuilder.Select("Id", "Title", "CreatedDate")
                .From("BlogPosts")
                .UnionAll(
                    SqlBuilder.Select("Id", "Title", "CreatedDate")
                        .From("GuestArticles")
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "INTERSECT - Find users with multiple purchases",
            "Using INTERSECT to find common results across multiple conditions",
            dialect => SqlBuilder.Select("UserId")
                .From("Orders")
                .Where("Year", 2024)
                .Intersect(
                    SqlBuilder.Select("UserId")
                        .From("Orders")
                        .Where("Amount", ComparisonOperator.GreaterThan, 500m)
                )
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "EXCEPT - Products not ordered",
            "Using EXCEPT to find products that were never ordered",
            dialect => SqlBuilder.Select("ProductId")
                .From("Products")
                .Except(
                    SqlBuilder.Select("ProductId")
                        .From("OrderItems")
                )
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


