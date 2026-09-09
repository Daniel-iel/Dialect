namespace Dialect.Core.AST.Migration;

/// <summary>
/// Base interface for migration operations.
/// </summary>
public abstract record MigrationStep;

/// <summary>
/// Creates a new table.
/// </summary>
public sealed record CreateTableStep(
    string TableName,
    IReadOnlyList<ColumnDef> Columns,
    IReadOnlyList<string>? PrimaryKeyColumns = null
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (Columns == null || Columns.Count == 0)
            throw new ArgumentException("At least one column must be specified");

        foreach (var col in Columns)
        {
            col.Validate();
        }

        // Validate primary key columns exist
        if (PrimaryKeyColumns != null)
        {
            foreach (var pkCol in PrimaryKeyColumns)
            {
                if (!Columns.Any(c => c.Name == pkCol))
                    throw new ArgumentException($"Primary key column '{pkCol}' does not exist in table definition");
            }
        }
    }
}

/// <summary>
/// Drops an existing table.
/// </summary>
public sealed record DropTableStep(
    string TableName,
    bool Cascade = false
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");
    }
}

/// <summary>
/// Adds a new column to a table.
/// </summary>
public sealed record AddColumnStep(
    string TableName,
    ColumnDef Column
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        Column.Validate();
    }
}

/// <summary>
/// Drops a column from a table.
/// </summary>
public sealed record DropColumnStep(
    string TableName,
    string ColumnName,
    bool Cascade = false
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (string.IsNullOrWhiteSpace(ColumnName))
            throw new ArgumentException("Column name cannot be empty");
    }
}

/// <summary>
/// Modifies an existing column.
/// </summary>
public sealed record AlterColumnStep(
    string TableName,
    string ColumnName,
    DataType? NewType = null,
    bool? Nullable = null,
    object? DefaultValue = null
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (string.IsNullOrWhiteSpace(ColumnName))
            throw new ArgumentException("Column name cannot be empty");

        // At least one modification should be specified
        if (NewType == null && Nullable == null && DefaultValue == null)
            throw new ArgumentException("At least one column attribute must be modified");
    }
}

/// <summary>
/// Creates an index on one or more columns.
/// </summary>
public sealed record AddIndexStep(
    string TableName,
    IReadOnlyList<string> ColumnNames,
    string? IndexName = null,
    bool IsUnique = false
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (ColumnNames == null || ColumnNames.Count == 0)
            throw new ArgumentException("At least one column must be specified for index");

        foreach (var col in ColumnNames)
        {
            if (string.IsNullOrWhiteSpace(col))
                throw new ArgumentException("Column names cannot be empty");
        }
    }
}

/// <summary>
/// Drops an index from a table.
/// </summary>
public sealed record DropIndexStep(
    string TableName,
    string IndexName
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (string.IsNullOrWhiteSpace(IndexName))
            throw new ArgumentException("Index name cannot be empty");
    }
}

/// <summary>
/// Adds a foreign key constraint.
/// </summary>
public sealed record AddForeignKeyStep(
    string TableName,
    string ColumnName,
    string ReferencedTable,
    string ReferencedColumn,
    string? ConstraintName = null,
    bool OnDeleteCascade = false
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (string.IsNullOrWhiteSpace(ColumnName))
            throw new ArgumentException("Column name cannot be empty");

        if (string.IsNullOrWhiteSpace(ReferencedTable))
            throw new ArgumentException("Referenced table name cannot be empty");

        if (string.IsNullOrWhiteSpace(ReferencedColumn))
            throw new ArgumentException("Referenced column name cannot be empty");
    }
}

/// <summary>
/// Drops a foreign key constraint.
/// </summary>
public sealed record DropForeignKeyStep(
    string TableName,
    string ConstraintName
) : MigrationStep
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("Table name cannot be empty");

        if (string.IsNullOrWhiteSpace(ConstraintName))
            throw new ArgumentException("Constraint name cannot be empty");
    }
}
