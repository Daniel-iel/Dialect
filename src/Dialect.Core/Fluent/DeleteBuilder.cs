namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Fluent builder for DELETE statements.
/// </summary>
public sealed class DeleteBuilder
{
    private TableReference? _table;
    private WhereExpression? _where;
    private bool _allowFullTableDelete;

    /// <summary>
    /// Sets the table to delete from.
    /// </summary>
    public DeleteBuilder From(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        _table = new TableReference(tableName, null, schema);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition.
    /// </summary>
    public DeleteBuilder Where(WhereExpression condition)
    {
        _where = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition with a simple comparison.
    /// </summary>
    public DeleteBuilder Where(string columnName, object? value)
    {
        var condition = new ComparisonNode(new Column(columnName), ComparisonOperator.Equal, value);
        return Where(condition);
    }

    /// <summary>
    /// Explicitly allows DELETE without WHERE clause (dangerous operation).
    /// </summary>
    public DeleteBuilder AllowFullTableOperation()
    {
        _allowFullTableDelete = true;
        return this;
    }

    /// <summary>
    /// Builds the immutable DeleteStatement.
    /// </summary>
    public DeleteStatement Build()
    {
        if (_table == null)
            throw new InvalidOperationException("Table must be specified via From().");
        
        if (_where == null && !_allowFullTableDelete)
            throw new InvalidOperationException("WHERE clause is required for DELETE. Use AllowFullTableOperation() if you intentionally want to delete all rows.");
        
        return new DeleteStatement(_table, _where, _allowFullTableDelete);
    }
}
