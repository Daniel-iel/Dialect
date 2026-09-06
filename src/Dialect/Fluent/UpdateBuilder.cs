namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Fluent builder for UPDATE statements.
/// </summary>
public sealed class UpdateBuilder
{
    private TableReference? _table;
    private readonly Dictionary<Column, object?> _setClauses = new();
    private WhereExpression? _where;
    private TableReference? _fromTable;
    private bool _allowFullTableUpdate;

    /// <summary>
    /// Sets the table to update.
    /// </summary>
    public UpdateBuilder Table(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        _table = new TableReference(tableName, null, schema);
        return this;
    }

    /// <summary>
    /// Adds a SET clause (column = value).
    /// </summary>
    public UpdateBuilder Set(string columnName, object? value)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));

        _setClauses[new Column(columnName)] = value;
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition.
    /// </summary>
    public UpdateBuilder Where(WhereExpression condition)
    {
        _where = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition with a simple comparison.
    /// </summary>
    public UpdateBuilder Where(string columnName, object? value)
    {
        var condition = new ComparisonNode(new Column(columnName), ComparisonOperator.Equal, value);
        return Where(condition);
    }

    /// <summary>
    /// Sets the FROM clause (SQL Server specific, but added here for completeness).
    /// </summary>
    public UpdateBuilder From(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        _fromTable = new TableReference(tableName, null, schema);
        return this;
    }

    /// <summary>
    /// Explicitly allows UPDATE without WHERE clause (dangerous operation).
    /// </summary>
    public UpdateBuilder AllowFullTableUpdate()
    {
        _allowFullTableUpdate = true;
        return this;
    }

    /// <summary>
    /// Builds the immutable UpdateStatement.
    /// </summary>
    public UpdateStatement Build()
    {
        if (_table == null)
            throw new InvalidOperationException("Table must be specified via Table().");

        if (_setClauses.Count == 0)
            throw new InvalidOperationException("At least one SET clause must be specified via Set().");

        if (_where == null && !_allowFullTableUpdate)
            throw new InvalidOperationException("WHERE clause is required for UPDATE. Use AllowFullTableUpdate() if you intentionally want to update all rows.");

        return new UpdateStatement(_table, _setClauses, _where, _fromTable, _allowFullTableUpdate);
    }
}
