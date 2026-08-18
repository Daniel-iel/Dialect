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

    private void NamingConventionValidation()
    {
        OutputFormatter.PrintSubHeader("Example 1: Naming Convention Validation");
        
        Console.WriteLine("  Validation Rules:");
        Console.WriteLine("    ✓ Table names: PascalCase or lowercase with underscores");
        Console.WriteLine("    ✓ Column names: PascalCase or snake_case");
        Console.WriteLine("    ✓ Primary key: {TableName}Id or id");
        Console.WriteLine("    ✓ Foreign key: {ReferencedTable}Id");
        
        Console.WriteLine("\n  Example Schema:");
        Console.WriteLine("    Table: Users (✓ Valid)");
        Console.WriteLine("      - UserId (✓ Valid PK)");
        Console.WriteLine("      - Username (✓ Valid)");
        Console.WriteLine("      - Email (✓ Valid)");
        Console.WriteLine("      - CreatedAt (✓ Valid)");
    }

    private void DataTypeValidation()
    {
        OutputFormatter.PrintSubHeader("Example 2: Data Type Validation");
        
        Console.WriteLine("  Validation Rules:");
        Console.WriteLine("    ✓ DECIMAL(10,2) for monetary values");
        Console.WriteLine("    ✓ VARCHAR/NVARCHAR for strings with length limit");
        Console.WriteLine("    ✓ INT or BIGINT for identifiers");
        Console.WriteLine("    ✓ TIMESTAMP/DATETIME for dates");
        
        Console.WriteLine("\n  Issues Found:");
        Console.WriteLine("    ⚠ Column 'Price' uses DECIMAL(10,2) ✓ Correct");
        Console.WriteLine("    ⚠ Column 'Total' uses DECIMAL(10,2) ✓ Correct");
        Console.WriteLine("    ✓ All monetary columns properly typed");
    }

    private void ConstraintValidation()
    {
        OutputFormatter.PrintSubHeader("Example 3: Constraint Validation");
        
        Console.WriteLine("  Validation Rules:");
        Console.WriteLine("    ✓ Primary keys: All tables must have");
        Console.WriteLine("    ✓ Foreign keys: Referential integrity");
        Console.WriteLine("    ✓ Not null: Critical columns");
        Console.WriteLine("    ✓ Unique: Natural keys (Email, Username)");
        
        Console.WriteLine("\n  Schema Constraints:");
        Console.WriteLine("    Table: Users");
        Console.WriteLine("      ✓ PK: UserId");
        Console.WriteLine("      ✓ UNIQUE: Email");
        Console.WriteLine("      ✓ NOT NULL: Username, Email");
        
        Console.WriteLine("\n    Table: Orders");
        Console.WriteLine("      ✓ PK: OrderId");
        Console.WriteLine("      ✓ FK: UserId -> Users(UserId)");
        Console.WriteLine("      ✓ NOT NULL: UserId, Total");
    }
}
