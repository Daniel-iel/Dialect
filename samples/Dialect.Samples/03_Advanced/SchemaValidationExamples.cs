namespace Dialect.Samples._03_Advanced;

using Dialect.Samples.Utilities;

/// <summary>
/// Schema validation examples demonstrating data type and constraint validation.
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

    private static void NamingConventionValidation()
    {
        OutputFormatter.PrintSubHeader("Example 1: Naming Convention Validation");

        const string namingValidationText = @"
  Validation Rules:
    ✓ Table names: PascalCase or lowercase with underscores
    ✓ Column names: PascalCase or snake_case
    ✓ Primary key: {TableName}Id or id
    ✓ Foreign key: {ReferencedTable}Id

  Example Schema:
    Table: Users (✓ Valid)
      - UserId (✓ Valid PK)
      - Username (✓ Valid)
      - Email (✓ Valid)
      - CreatedAt (✓ Valid)";

        Console.WriteLine(namingValidationText);
    }

    private static void DataTypeValidation()
    {
        OutputFormatter.PrintSubHeader("Example 2: Data Type Validation");

        const string dataTypeValidationText = @"
  Validation Rules:
    ✓ DECIMAL(10,2) for monetary values
    ✓ VARCHAR/NVARCHAR for strings with length limit
    ✓ INT or BIGINT for identifiers
    ✓ TIMESTAMP/DATETIME for dates

  Issues Found:
    ⚠ Column 'Price' uses DECIMAL(10,2) ✓ Correct
    ⚠ Column 'Total' uses DECIMAL(10,2) ✓ Correct
    ✓ All monetary columns properly typed";

        Console.WriteLine(dataTypeValidationText);
    }

    private static void ConstraintValidation()
    {
        OutputFormatter.PrintSubHeader("Example 3: Constraint Validation");

        const string constraintValidationText = @"
  Validation Rules:
    ✓ Primary keys: All tables must have
    ✓ Foreign keys: Referential integrity
    ✓ Not null: Critical columns
    ✓ Unique: Natural keys (Email, Username)

  Schema Constraints:
    Table: Users
      ✓ PK: UserId
      ✓ UNIQUE: Email
      ✓ NOT NULL: Username, Email

    Table: Orders
      ✓ PK: OrderId
      ✓ FK: UserId -> Users(UserId)
      ✓ NOT NULL: UserId, Total";

        Console.WriteLine(constraintValidationText);
    }
}
