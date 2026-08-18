namespace Dialect.Core.Fluent;

using Dialect.Core.AST;
using System.Collections.Generic;

/// <summary>
/// Fluent builder for SELECT statements.
/// State machine that tracks WHERE conditions and builds immutable SelectStatement on Build().
/// </summary>
public sealed class SelectBuilder
{
    private readonly List<Column> _columns;
    private TableReference? _from;
    private readonly List<JoinClause> _joins = new();
    private WhereExpression? _where;
    private readonly List<Column> _groupByColumns = new();
    private WhereExpression? _having;
    private readonly List<OrderByClause> _orderByClauses = new();
    private RowLimit? _rowLimit;
    private bool _isDistinct;
    private readonly Dictionary<Type, object> _dialectExtensions = new();
    private readonly List<WithClause> _withClauses = new();
    private readonly List<WindowFunction> _windowFunctions = new();

    internal SelectBuilder(List<Column> columns)
    {
        _columns = columns ?? throw new ArgumentNullException(nameof(columns));
    }

    /// <summary>
    /// Adds a Common Table Expression (CTE).
    /// </summary>
    public SelectBuilder With(string cteName, SelectStatement query, IReadOnlyList<string>? columnNames = null)
    {
        if (string.IsNullOrWhiteSpace(cteName))
            throw new ArgumentException("CTE name cannot be empty", nameof(cteName));
        
        _withClauses.Add(new WithClause(cteName, query, columnNames));
        return this;
    }

    /// <summary>
    /// Adds a Common Table Expression (CTE) using a builder.
    /// </summary>
    public SelectBuilder With(string cteName, SelectBuilder queryBuilder, IReadOnlyList<string>? columnNames = null)
    {
        if (string.IsNullOrWhiteSpace(cteName))
            throw new ArgumentException("CTE name cannot be empty", nameof(cteName));
        
        var query = queryBuilder.Build();
        _withClauses.Add(new WithClause(cteName, query, columnNames));
        return this;
    }

    /// <summary>
    /// Sets the FROM clause.
    /// </summary>
    public SelectBuilder From(string tableName, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        _from = new TableReference(tableName, alias, schema);
        return this;
    }

    /// <summary>
    /// Sets the FROM clause with a table reference.
    /// </summary>
    public SelectBuilder From(TableReference table)
    {
        _from = table ?? throw new ArgumentNullException(nameof(table));
        return this;
    }

    /// <summary>
    /// Sets the FROM clause with a subquery.
    /// </summary>
    public SelectBuilder From(SelectStatement subquery, string alias)
    {
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Subquery alias cannot be empty", nameof(alias));
        
        _from = new TableReference("query", alias, null, subquery);
        return this;
    }

    /// <summary>
    /// Sets the FROM clause with a subquery builder.
    /// </summary>
    public SelectBuilder From(SelectBuilder subqueryBuilder, string alias)
    {
        if (subqueryBuilder == null)
            throw new ArgumentNullException(nameof(subqueryBuilder));
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Subquery alias cannot be empty", nameof(alias));
        
        var subquery = subqueryBuilder.Build();
        _from = new TableReference("query", alias, null, subquery);
        return this;
    }

    /// <summary>
    /// Adds an INNER JOIN clause.
    /// </summary>
    public SelectBuilder InnerJoin(string tableName, WhereExpression onCondition, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        var table = new TableReference(tableName, alias, schema);
        _joins.Add(new JoinClause(table, JoinType.Inner, onCondition));
        return this;
    }

    /// <summary>
    /// Adds a LEFT JOIN clause.
    /// </summary>
    public SelectBuilder LeftJoin(string tableName, WhereExpression onCondition, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        var table = new TableReference(tableName, alias, schema);
        _joins.Add(new JoinClause(table, JoinType.Left, onCondition));
        return this;
    }

    /// <summary>
    /// Adds a RIGHT JOIN clause.
    /// </summary>
    public SelectBuilder RightJoin(string tableName, WhereExpression onCondition, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        var table = new TableReference(tableName, alias, schema);
        _joins.Add(new JoinClause(table, JoinType.Right, onCondition));
        return this;
    }

    /// <summary>
    /// Adds a FULL OUTER JOIN clause.
    /// Note: Not supported natively in MySQL; will throw at Compile() time if using MySQL dialect.
    /// </summary>
    public SelectBuilder FullJoin(string tableName, WhereExpression onCondition, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        var table = new TableReference(tableName, alias, schema);
        _joins.Add(new JoinClause(table, JoinType.Full, onCondition));
        return this;
    }

    /// <summary>
    /// Adds a CROSS JOIN clause (no ON condition).
    /// </summary>
    public SelectBuilder CrossJoin(string tableName, string? alias = null, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        var table = new TableReference(tableName, alias, schema);
        _joins.Add(new JoinClause(table, JoinType.Cross, null));
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition using AND logic if WHERE already exists.
    /// </summary>
    public SelectBuilder Where(WhereExpression condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        
        _where = _where == null ? condition : new AndNode(_where, condition);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition with a simple column = value comparison.
    /// </summary>
    public SelectBuilder Where(string columnName, object? value)
    {
        var condition = new ComparisonNode(new Column(columnName), ComparisonOperator.Equal, value);
        return Where(condition);
    }

    /// <summary>
    /// Adds a WHERE condition with a comparison operator.
    /// </summary>
    public SelectBuilder Where(string columnName, ComparisonOperator op, object? value)
    {
        var condition = new ComparisonNode(new Column(columnName), op, value);
        return Where(condition);
    }

    /// <summary>
    /// Adds an OR condition to existing WHERE.
    /// </summary>
    public SelectBuilder OrWhere(WhereExpression condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        
        if (_where == null)
            throw new InvalidOperationException("Cannot use OrWhere() without an existing Where() condition.");
        
        _where = new OrNode(_where, condition);
        return this;
    }

    /// <summary>
    /// Adds raw SQL to WHERE clause (escape hatch, always parametrized).
    /// </summary>
    public SelectBuilder WhereRaw(string sqlFragment, Dictionary<string, object?>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(sqlFragment))
            throw new ArgumentException("SQL fragment cannot be empty", nameof(sqlFragment));
        
        var rawCondition = new RawNode(sqlFragment, parameters);
        return Where(rawCondition);
    }

    /// <summary>
    /// Adds GROUP BY columns.
    /// </summary>
    public SelectBuilder GroupBy(params string[] columnNames)
    {
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columnNames));
        
        _groupByColumns.AddRange(columnNames.Select(c => new Column(c)));
        return this;
    }

    /// <summary>
    /// Adds a HAVING condition (filter on grouped results).
    /// </summary>
    public SelectBuilder Having(WhereExpression condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        
        _having = condition;
        return this;
    }

    /// <summary>
    /// Adds a window function to the SELECT clause.
    /// </summary>
    public SelectBuilder SelectWindow(
        string functionName,
        IReadOnlyList<string>? args = null,
        OverClause? overClause = null,
        string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));
        
        var windowFunc = new WindowFunction(functionName, args, overClause, alias);
        windowFunc.Validate();
        
        _windowFunctions.Add(windowFunc);
        return this;
    }

    /// <summary>
    /// Adds a window function with PARTITION BY specification.
    /// </summary>
    public SelectBuilder SelectWindow(
        string functionName,
        IReadOnlyList<string> partitionByColumns,
        IReadOnlyList<OrderByClause>? orderByItems = null,
        string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));
        
        var overClause = new OverClause(partitionByColumns, orderByItems);
        var windowFunc = new WindowFunction(functionName, null, overClause, alias);
        windowFunc.Validate();
        
        _windowFunctions.Add(windowFunc);
        return this;
    }

    /// <summary>
    /// Adds an analytical window function (LAG, LEAD, etc.) with a column argument.
    /// </summary>
    public SelectBuilder SelectWindowAnalytical(
        string functionName,
        string columnArg,
        IReadOnlyList<string>? partitionByColumns = null,
        IReadOnlyList<OrderByClause>? orderByItems = null,
        string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));
        if (string.IsNullOrWhiteSpace(columnArg))
            throw new ArgumentException("Column argument cannot be empty", nameof(columnArg));
        
        var overClause = new OverClause(partitionByColumns, orderByItems);
        var windowFunc = new WindowFunction(functionName, new[] { columnArg }, overClause, alias);
        windowFunc.Validate();
        
        _windowFunctions.Add(windowFunc);
        return this;
    }

    /// <summary>
    /// Adds ORDER BY clause.
    /// </summary>
    public SelectBuilder OrderBy(string columnName, SortDirection direction = SortDirection.Ascending)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));
        
        _orderByClauses.Add(new OrderByClause(new Column(columnName), direction));
        return this;
    }

    /// <summary>
    /// Adds ORDER BY in ascending order.
    /// </summary>
    public SelectBuilder ThenBy(string columnName)
    {
        return OrderBy(columnName, SortDirection.Ascending);
    }

    /// <summary>
    /// Adds ORDER BY in descending order.
    /// </summary>
    public SelectBuilder OrderByDescending(string columnName)
    {
        return OrderBy(columnName, SortDirection.Descending);
    }

    /// <summary>
    /// Sets pagination using LIMIT/OFFSET or TOP.
    /// </summary>
    public SelectBuilder Take(int count, int? offset = null)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be greater than 0", nameof(count));
        
        _rowLimit = new RowLimit(count, offset);
        return this;
    }

    /// <summary>
    /// Sets the OFFSET for pagination.
    /// </summary>
    public SelectBuilder Skip(int offset)
    {
        if (offset < 0)
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        
        if (_rowLimit == null)
            throw new InvalidOperationException("Skip() requires Take() to be called first.");
        
        _rowLimit = new RowLimit(_rowLimit.Count, offset);
        return this;
    }

    /// <summary>
    /// Enables DISTINCT for result deduplication.
    /// </summary>
    public SelectBuilder Distinct()
    {
        _isDistinct = true;
        return this;
    }

    /// <summary>
    /// Adds a dialect-specific extension.
    /// </summary>
    public SelectBuilder WithDialectExtension<T>(T extension) where T : class
    {
        _dialectExtensions[typeof(T)] = extension;
        return this;
    }

    /// <summary>
    /// Builds the immutable SelectStatement.
    /// </summary>
    public SelectStatement Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("At least one column must be selected.");
        
        return new SelectStatement(
            _columns,
            _from,
            _joins.Count > 0 ? _joins : null,
            _where,
            _groupByColumns.Count > 0 ? _groupByColumns : null,
            _having,
            _orderByClauses.Count > 0 ? _orderByClauses : null,
            _rowLimit,
            _isDistinct,
            _dialectExtensions.Count > 0 ? _dialectExtensions : null,
            _withClauses.Count > 0 ? _withClauses : null,
            _windowFunctions.Count > 0 ? _windowFunctions : null);
    }
}
