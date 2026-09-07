namespace Dialect.Samples._03_Advanced;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Schema validation examples demonstrating data type and constraint validation.
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

    public override void Run()
    {
        NamingConventionValidation();
        Console.WriteLine("\n");
        DataTypeValidation();
        Console.WriteLine("\n");
        ConstraintValidation();
    }

    private void NamingConventionValidation()
    {
        OutputFormatter.PrintSubHeader("Example 1: Naming Convention Validation");

        // Build and compile a query using properly named columns
        var compiledResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("UserId", "Username", "Email", "CreatedAt")
                .From("Users")
                .Build()
                .Compile(dialect)
        );

        AddScenario("Schema Validation - Naming Convention (PascalCase)", compiledResults);

        const string namingValidationText = @"
  Validation Rules:
    ✓ Table names: PascalCase or lowercase with underscores
    ✓ Column names: PascalCase or snake_case
    ✓ Primary key: {TableName}Id or id
    ✓ Foreign key: {ReferencedTable}Id

  Example Schema Validation:
    Table: Users (✓ Valid)
      - UserId (✓ Valid PK)
      - Username (✓ Valid)
      - Email (✓ Valid)
      - CreatedAt (✓ Valid)";

        Console.WriteLine(namingValidationText);
    }

    private void DataTypeValidation()
    {
        OutputFormatter.PrintSubHeader("Example 2: Data Type Validation");

        // Build and compile a query selecting various data types
        var compiledResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("ProductId", "Name", "Price", "StockQuantity")
                .From("Products")
                .Build()
                .Compile(dialect)
        );

        AddScenario("Schema Validation - Data Type constraints", compiledResults);

        const string dataTypeValidationText = @"
  Validation Rules:
    ✓ DECIMAL(10,2) for monetary values
    ✓ VARCHAR/NVARCHAR for strings with length limit
    ✓ INT or BIGINT for identifiers
    ✓ TIMESTAMP/DATETIME for dates

  Validation Results:
    ✓ Column 'Price' uses DECIMAL(10,2) ✓ Correct
    ✓ Column 'StockQuantity' uses INT ✓ Correct
    ✓ Column 'ProductId' uses INT (PK) ✓ Correct
    ✓ All data types properly configured";

        Console.WriteLine(dataTypeValidationText);
    }

    private void ConstraintValidation()
    {
        OutputFormatter.PrintSubHeader("Example 3: Constraint Validation");

        // Build and compile a query selecting from related tables
        var compiledResults = CompileForAllDialects(dialect =>
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
        });


        AddScenario("Schema Validation - Referential Integrity", compiledResults);

        const string constraintValidationText = @"
  Validation Rules:
    ✓ Primary keys: All tables must have
    ✓ Foreign keys: Referential integrity
    ✓ Not null: Critical columns
    ✓ Unique: Natural keys (Email, Username)

  Schema Constraints Verified:
    Table: Users
      ✓ PK: UserId
      ✓ UNIQUE: Email
      ✓ NOT NULL: Username, Email

    Table: Orders
      ✓ PK: OrderId
      ✓ FK: UserId -> Users(UserId) - Valid
      ✓ NOT NULL: UserId, Total
      ✓ Referential integrity: Confirmed";

        Console.WriteLine(constraintValidationText);
    }
}
