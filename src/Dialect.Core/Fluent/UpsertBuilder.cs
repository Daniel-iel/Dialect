namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Fluent builder for UPSERT statements.
/// Allows building INSERT ... ON CONFLICT/DUPLICATE KEY UPDATE queries.
/// </summary>
public sealed class UpsertBuilder
{
    private readonly TableReference _table;
    private readonly List<Column> _columns = new();
    private List<object?>? _values;
    private List<string>? _conflictColumns;
    private readonly List<UpsertUpdateClause> _updateClauses = new();

    internal UpsertBuilder(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        _table = new TableReference(tableName, null, schema);
    }

    /// <summary>
    /// Adds columns to the UPSERT statement.
    /// </summary>
    public UpsertBuilder Columns(params string[] columnNames)
    {
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columnNames));
        
        foreach (var column in columnNames)
        {
            if (string.IsNullOrWhiteSpace(column))
                throw new ArgumentException("Column name cannot be empty");
            _columns.Add(new Column(column));
        }
        
        return this;
    }

    /// <summary>
    /// Sets the values to insert.
    /// Must match the number and order of columns.
    /// </summary>
    public UpsertBuilder Values(params object?[] values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (values.Length != _columns.Count)
            throw new ArgumentException(
                $"Value count ({values.Length}) must match column count ({_columns.Count})", 
                nameof(values));
        
        _values = new List<object?>(values);
        return this;
    }

    /// <summary>
    /// Specifies the conflict column(s) for ON CONFLICT clause.
    /// </summary>
    public UpsertBuilder OnConflict(params string[] columnNames)
    {
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one conflict column must be specified", nameof(columnNames));
        
        _conflictColumns = new List<string>(columnNames);
        return this;
    }

    /// <summary>
    /// Adds an update clause for the conflict resolution.
    /// Specifies which column should be updated with which value when a conflict occurs.
    /// </summary>
    public UpsertBuilder UpdateSet(string columnName, object? value)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));
        
        _updateClauses.Add(new UpsertUpdateClause(columnName, value));
        return this;
    }

    /// <summary>
    /// Builds the immutable UpsertStatement.
    /// </summary>
    public UpsertStatement Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be specified");
        
        if (_values == null || _values.Count == 0)
            throw new InvalidOperationException("Values must be specified");
        
        if (_updateClauses.Count == 0)
            throw new InvalidOperationException("At least one update clause must be specified");
        
        var conflictClause = _conflictColumns != null && _conflictColumns.Count > 0
            ? new UpsertConflictClause(_conflictColumns, _updateClauses)
            : new UpsertConflictClause(null, _updateClauses);
        
        var statement = new UpsertStatement(_table, _columns, _values, conflictClause);
        statement.Validate();
        
        return statement;
    }
}
