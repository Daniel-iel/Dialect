namespace Dialect.Core.AST;

/// <summary>
/// Base class for all AST query nodes.
/// </summary>
public abstract record QueryNode;

/// <summary>
/// Base class for WHERE/HAVING/JOIN ON expressions, supporting AND/OR/comparison trees.
/// </summary>
public abstract record WhereExpression : QueryNode;

/// <summary>
/// Represents an AND condition combining two expressions.
/// </summary>
public sealed record AndNode(WhereExpression Left, WhereExpression Right) : WhereExpression;

/// <summary>
/// Represents an OR condition combining two expressions.
/// </summary>
public sealed record OrNode(WhereExpression Left, WhereExpression Right) : WhereExpression;

/// <summary>
/// Represents a comparison (=, <>, <, >, etc.) between a column and value/parameter.
/// </summary>
public sealed record ComparisonNode(
    Column Column,
    ComparisonOperator Operator,
    object? Value) : WhereExpression;

/// <summary>
/// Represents an IN condition (column IN (value1, value2, ...)) or IN with subquery (column IN (SELECT ...)).
/// Either Values or SubquerySource must be provided, but not both.
/// </summary>
public sealed record InNode(
    Column Column,
    IReadOnlyList<object?>? Values = null,
    SelectStatement? SubquerySource = null,
    bool Negated = false) : WhereExpression;

/// <summary>
/// Represents raw SQL with parameters, for escape hatch scenarios.
/// Heuristic validation to detect suspicious patterns.
/// </summary>
public sealed record RawNode(
    string SqlFragment,
    Dictionary<string, object?>? Parameters = null) : WhereExpression;

/// <summary>
/// Represents a function call in expressions (e.g., in WHERE, SELECT, ORDER BY).
/// </summary>
public sealed record FunctionCallNode(
    string FunctionName,
    IReadOnlyList<object?>? Arguments = null) : WhereExpression;

/// <summary>
/// Represents a JOIN clause.
/// </summary>
public sealed record JoinClause(
    TableReference Table,
    JoinType Type,
    WhereExpression? OnCondition = null);
