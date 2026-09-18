namespace Dialect.Samples._03_Advanced;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Schema validation examples demonstrating data type and constraint validation.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// 
/// NOTE: This is an educational example showing how the framework can validate schema 
/// naming conventions, data types, and referential integrity patterns.
/// 
/// The validation rules shown are best practices commonly used in SQL Server, PostgreSQL, 
/// and MySQL. In production scenarios, these validations would be applied to actual database
/// schemas to identify deviations from organizational standards.
/// </summary>
public class SchemaValidationExamples : ExampleBase
{
    public SchemaValidationExamples() : base(
        "Schema Validation",
        "Demonstrates schema validation rules and best practices")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "Schema Validation - Naming Convention (PascalCase)",
            "Demonstrates naming convention validation for properly named tables and columns",
            dialect => SqlBuilder.Select("UserId", "Username", "Email", "CreatedAt")
                .From("Users")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Schema Validation - Data Type constraints",
            "Demonstrates data type validation for various column types",
            dialect => SqlBuilder.Select("ProductId", "Name", "Price", "StockQuantity")
                .From("Products")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "Schema Validation - Referential Integrity",
            "Demonstrates referential integrity validation through foreign key relationships",
            dialect =>
            {
                var joinCondition = new ComparisonNode(
                    new Column("o.UserId"),
                    ComparisonOperator.Equal,
                    new Column("u.UserId")
                );
                return SqlBuilder.Select("OrderId", "UserId", "Total", "Email")
                    .From("Orders o")
                    .InnerJoin("Users u", joinCondition)
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

        // Educational validation output
        Console.WriteLine("\n  Schema Validation Summary:");
        Console.WriteLine("    ✓ Naming conventions: PascalCase for tables and columns");
        Console.WriteLine("    ✓ Data types: DECIMAL for money, INT for IDs, VARCHAR for strings");
        Console.WriteLine("    ✓ Constraints: Primary keys, foreign keys, NOT NULL, UNIQUE");
    }
}
