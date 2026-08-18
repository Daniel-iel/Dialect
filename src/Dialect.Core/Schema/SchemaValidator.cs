namespace Dialect.Core.Schema;

using Dialect.Core.AST.Migration;
using Dialect.Core.Dialects;

/// <summary>
/// Abstract base for schema validation across all SQL dialects.
/// Validates migration definitions, column constraints, and naming conventions.
/// </summary>
public abstract class SchemaValidator
{
    /// <summary>
    /// Validates a complete migration definition against the target dialect.
    /// </summary>
    public abstract ValidationResult Validate(Migration migration, ISqlDialect dialect);

    /// <summary>
    /// Validates a single column definition against dialect constraints.
    /// </summary>
    public abstract ValidationResult ValidateColumn(ColumnDef column, ISqlDialect dialect);

    /// <summary>
    /// Validates table metadata (name, columns, constraints).
    /// </summary>
    public abstract ValidationResult ValidateTable(string tableName, ColumnDef[] columns, ISqlDialect dialect);

    /// <summary>
    /// Validates a migration step.
    /// </summary>
    public abstract ValidationResult ValidateStep(MigrationStep step, ISqlDialect dialect);

    /// <summary>
    /// Gets the naming convention rule for this dialect (e.g., PascalCase, snake_case).
    /// </summary>
    public abstract NamingConvention NamingConvention { get; }

    /// <summary>
    /// Gets the maximum identifier length for this dialect.
    /// </summary>
    public abstract int MaxIdentifierLength { get; }
}

/// <summary>
/// Naming convention rules for identifiers.
/// </summary>
public enum NamingConvention
{
    /// <summary>PascalCase for tables/columns (UserAccounts, FirstName)</summary>
    PascalCase,

    /// <summary>snake_case for tables/columns (user_accounts, first_name)</summary>
    SnakeCase,

    /// <summary>camelCase for columns, PascalCase for tables</summary>
    Mixed,

    /// <summary>No enforcement</summary>
    Any
}

