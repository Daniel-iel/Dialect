namespace Dialect.Core.AST;

/// <summary>
/// Represents a column reference, optionally with an alias.
/// </summary>
public sealed record Column(
    string Name,
    string? Alias = null,
    string? TableAlias = null)
{
    /// <summary>
    /// Gets the display name for this column (alias if present, else Name).
    /// </summary>
    public string DisplayName => Alias ?? Name;
};

/// <summary>
/// Represents a table or subquery reference with optional alias.
/// </summary>
public sealed record TableReference(
    string Name,
    string? Alias = null,
    string? Schema = null,
    SelectStatement? SubquerySource = null)
{
    /// <summary>
    /// Gets the display name for this table (alias if present, else Name).
    /// </summary>
    public string DisplayName => Alias ?? Name;
};

/// <summary>
/// Represents an ORDER BY clause component.
/// </summary>
public sealed record OrderByClause(
    Column Column,
    SortDirection Direction = SortDirection.Ascending);

/// <summary>
/// Represents pagination settings (LIMIT/OFFSET or TOP).
/// </summary>
public sealed record RowLimit(
    int Count,
    int? Offset = null,
    bool WithTies = false);

/// <summary>
/// Represents a parameter for a routine (procedure/function).
/// </summary>
public sealed record RoutineParameter(
    string Name,
    object? Value = null,
    ParameterDirection Direction = ParameterDirection.Input,
    string? Type = null);

/// <summary>
/// Compiled SQL query with text and parameters.
/// </summary>
public sealed record CompiledQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters);
