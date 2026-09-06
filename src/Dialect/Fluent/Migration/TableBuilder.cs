namespace Dialect.Core.Fluent.Migration;

using Dialect.Core.AST.Migration;

/// <summary>
/// Fluent builder for defining a table in a migration.
/// </summary>
public sealed class TableBuilder
{
    private readonly string _tableName;
    private readonly List<ColumnDef> _columns = new();
    private List<string>? _primaryKeyColumns;

    internal TableBuilder(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        _tableName = tableName;
    }

    /// <summary>
    /// Defines a new column in the table.
    /// </summary>
    public ColumnBuilder Column(string columnName)
    {
        return new ColumnBuilder(columnName);
    }

    /// <summary>
    /// Adds a pre-built column to the table.
    /// </summary>
    internal TableBuilder AddColumn(ColumnDef column)
    {
        _columns.Add(column);
        return this;
    }

    /// <summary>
    /// Defines the primary key for the table.
    /// </summary>
    public TableBuilder PrimaryKey(params string[] columnNames)
    {
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one column must be specified for primary key", nameof(columnNames));

        _primaryKeyColumns = new List<string>(columnNames);
        return this;
    }

    /// <summary>
    /// Builds the immutable CreateTableStep.
    /// </summary>
    internal CreateTableStep Build()
    {
        var step = new CreateTableStep(
            TableName: _tableName,
            Columns: _columns.AsReadOnly(),
            PrimaryKeyColumns: _primaryKeyColumns?.AsReadOnly()
        );

        step.Validate();
        return step;
    }
}
