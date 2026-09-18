using System.Collections.Immutable;

namespace Dialect.Core.AST;

/// <summary>
/// Represents a SELECT statement with full support for clauses.
/// Immutable after construction.
/// </summary>
public sealed record SelectStatement(
    IReadOnlyList<Column> Columns,
    TableReference? From = null,
    IReadOnlyList<JoinClause>? Joins = null,
    WhereExpression? Where = null,
    IReadOnlyList<Column>? GroupByColumns = null,
    WhereExpression? Having = null,
    IReadOnlyList<OrderByClause>? OrderByClauses = null,
    RowLimit? RowLimit = null,
    bool IsDistinct = false,
    IReadOnlyDictionary<Type, object>? DialectExtensions = null,
    IReadOnlyList<WithClause>? WithClauses = null,
    IReadOnlyList<WindowFunction>? WindowFunctions = null) : QueryNode
{
    public IReadOnlyList<JoinClause> Joins { get; } = Joins ?? Array.Empty<JoinClause>();
    public IReadOnlyList<Column> GroupByColumns { get; } = GroupByColumns ?? Array.Empty<Column>();
    public IReadOnlyList<OrderByClause> OrderByClauses { get; } = OrderByClauses ?? Array.Empty<OrderByClause>();
    public IReadOnlyDictionary<Type, object> DialectExtensions { get; } = DialectExtensions ?? new Dictionary<Type, object>();
    public IReadOnlyList<WithClause> WithClauses { get; } = WithClauses ?? Array.Empty<WithClause>();
    public IReadOnlyList<WindowFunction> WindowFunctions { get; } = WindowFunctions ?? Array.Empty<WindowFunction>();
};

/// <summary>
/// Represents an INSERT statement.
/// </summary>
public sealed record InsertStatement(
    TableReference Table,
    IReadOnlyList<Column> Columns,
    IReadOnlyList<IReadOnlyList<object?>>? Values = null,
    SelectStatement? SelectSource = null) : QueryNode;

/// <summary>
/// Represents an UPDATE statement.
/// </summary>
public sealed record UpdateStatement(
    TableReference Table,
    IReadOnlyDictionary<Column, object?> SetClauses,
    WhereExpression? Where = null,
    TableReference? FromTable = null,
    bool AllowFullTableUpdate = false) : QueryNode;

/// <summary>
/// Represents a DELETE statement.
/// </summary>
public sealed record DeleteStatement(
    TableReference Table,
    WhereExpression? Where = null,
    bool AllowFullTableDelete = false) : QueryNode;

/// <summary>
/// Represents a compound SELECT statement combining two or more SELECT statements with set operators (UNION, INTERSECT, EXCEPT).
/// Supports chaining multiple operations: (SELECT ... UNION SELECT ... INTERSECT SELECT ...)
/// </summary>
public sealed record CompoundSelectStatement(
    SelectStatement Left,
    SetOperator Operator,
    SelectStatement Right,
    CompoundSelectStatement? Next = null) : QueryNode;

/// <summary>
/// Represents a call to a stored procedure or function.
/// </summary>
public sealed record RoutineCall(
    string Name,
    RoutineKind Kind,
    IReadOnlyList<RoutineParameter>? Parameters = null,
    string? Schema = null,
    string? Package = null) : QueryNode
{
    public IReadOnlyList<RoutineParameter> Parameters { get; } = Parameters ?? Array.Empty<RoutineParameter>();
};
