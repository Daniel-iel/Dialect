namespace Dialect.Core.AST.Migration;

/// <summary>
/// Represents a column definition in a migration.
/// </summary>
public sealed record ColumnDef(
    string Name,
    DataType Type,
    bool Nullable = true,
    object? DefaultValue = null,
    bool IsAutoIncrement = false,
    bool IsPrimaryKey = false,
    int? Length = null,        // For VARCHAR(n), CHAR(n)
    (int Precision, int Scale)? Precision = null  // For DECIMAL(p, s)
)
{
    /// <summary>
    /// Validates the column definition for correctness.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Column name cannot be empty");

        // AutoIncrement requires NOT NULL
        if (IsAutoIncrement && Nullable)
            throw new ArgumentException("AUTO_INCREMENT columns must be NOT NULL");

        // PrimaryKey requires NOT NULL
        if (IsPrimaryKey && Nullable)
            throw new ArgumentException("PRIMARY KEY columns must be NOT NULL");

        // String types should have length if VARCHAR, CHAR
        if ((Type == DataType.Varchar || Type == DataType.Char ||
             Type == DataType.NVarchar || Type == DataType.NChar) && Length == null)
            throw new ArgumentException($"{Type} requires a length specification");

        // Decimal should have precision
        if (Type == DataType.Decimal && Precision == null)
            throw new ArgumentException("DECIMAL requires precision and scale");
    }
}
