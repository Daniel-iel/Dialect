namespace Dialect.Core.AST;

/// <summary>
/// Represents a Common Table Expression (CTE) clause.
/// Used in WITH statements: WITH cte_name AS (SELECT ...) SELECT ...
/// </summary>
public sealed record WithClause(
    string Name,
    SelectStatement Query,
    IReadOnlyList<string>? ColumnNames = null);
