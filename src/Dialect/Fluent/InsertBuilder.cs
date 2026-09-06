namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Fluent builder for INSERT statements.
/// </summary>
public sealed class InsertBuilder
{
    private TableReference? _table;
    private readonly List<Column> _columns = new();
    private readonly List<IReadOnlyList<object?>> _values = new();
    private SelectStatement? _selectSource;

    /// <summary>
    /// Sets the table to insert into.
    /// </summary>
    public InsertBuilder Into(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        _table = new TableReference(tableName, null, schema);
        return this;
    }

    /// <summary>
    /// Specifies columns for the INSERT.
    /// </summary>
    public InsertBuilder Columns(params string[] columnNames)
    {
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columnNames));

        _columns.AddRange(columnNames.Select(c => new Column(c)));
        return this;
    }

    /// <summary>
    /// Adds a row of values to insert.
    /// </summary>
    public InsertBuilder Values(params object?[] rowValues)
    {
        if (rowValues == null || rowValues.Length == 0)
            throw new ArgumentException("At least one value must be specified", nameof(rowValues));

        _values.Add(rowValues);
        return this;
    }

    /// <summary>
    /// Sets the INSERT source to a SELECT statement.
    /// </summary>
    public InsertBuilder Select(SelectStatement select)
    {
        _selectSource = select ?? throw new ArgumentNullException(nameof(select));
        return this;
    }

    /// <summary>
    /// Builds the immutable InsertStatement.
    /// </summary>
    public InsertStatement Build()
    {
        if (_table == null)
            throw new InvalidOperationException("Table must be specified via Into().");

        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be specified via Columns().");

        if (_values.Count == 0 && _selectSource == null)
            throw new InvalidOperationException("Either Values() or Select() must be specified.");

        return new InsertStatement(_table, _columns, _values.Count > 0 ? _values : null, _selectSource);
    }
}
