namespace Dialect.Core.AST;

/// <summary>
/// Enumerates all supported SQL dialects.
/// </summary>
public enum SqlProvider
{
    SqlServer,
    PostgreSql,
    MySql
}

/// <summary>
/// Enumerates supported SQL JOIN types.
/// </summary>
public enum JoinType
{
    Inner,
    Left,
    Right,
    Full,
    Cross
}

/// <summary>
/// Enumerates SQL sort directions.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Enumerates SQL features that may vary by dialect.
/// Dialects declare which features they support via ISqlDialect.Supports().
/// This enum grows additively without breaking existing implementations.
/// </summary>
public enum SqlFeature
{
    /// <summary>FULL OUTER JOIN — not supported natively in MySQL.</summary>
    FullJoin,

    /// <summary>RETURNING clause — PostgreSQL specific in Phase 1.</summary>
    Returning,

    /// <summary>Window functions (ROW_NUMBER OVER, etc.).</summary>
    WindowFunctions,

    /// <summary>CTEs / WITH clause.</summary>
    CommonTableExpressions,

    /// <summary>JSON operations (JSON_VALUE, ->, etc.).</summary>
    JsonOperations,

    /// <summary>UPSERT / Merge operations.</summary>
    Upsert,

    /// <summary>Stored procedures.</summary>
    StoredProcedures,

    /// <summary>Scalar and table-valued functions.</summary>
    Functions
}

/// <summary>
/// Enumerates kinds of routine (procedure vs function).
/// </summary>
public enum RoutineKind
{
    Procedure,
    Function
}

/// <summary>
/// Enumerates parameter directions for routines.
/// </summary>
public enum ParameterDirection
{
    Input,
    Output,
    InputOutput
}

/// <summary>
/// Enumerates SQL comparison operators.
/// </summary>
public enum ComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Like,
    NotLike,
    In,
    NotIn,
    IsNull,
    IsNotNull,
    Between,
    NotBetween
}

/// <summary>
/// Enumerates set operations for combining SELECT statements.
/// </summary>
public enum SetOperator
{
    /// <summary>UNION — combines results and removes duplicates.</summary>
    Union,

    /// <summary>UNION ALL — combines results, keeping all rows including duplicates.</summary>
    UnionAll,

    /// <summary>INTERSECT — returns only rows that appear in both result sets.</summary>
    Intersect,

    /// <summary>EXCEPT — returns rows from left set that don't appear in right set.</summary>
    Except
}
