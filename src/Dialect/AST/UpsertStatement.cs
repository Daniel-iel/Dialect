namespace Dialect.Core.AST;

/// <summary>
/// Represents a single column-value pair for UPSERT conflict resolution.
/// Used in ON CONFLICT DO UPDATE / ON DUPLICATE KEY UPDATE clauses.
/// </summary>
public sealed record UpsertUpdateClause(
    string ColumnName,
    object? Value
);

/// <summary>
/// Represents the conflict/duplicate key handling strategy for UPSERT operations.
/// Different databases use different approaches:
/// - SQL Server: MERGE with WHEN MATCHED THEN UPDATE
/// - PostgreSQL: ON CONFLICT (columns) DO UPDATE SET
/// - MySQL: ON DUPLICATE KEY UPDATE
/// </summary>
public sealed record UpsertConflictClause(
    IReadOnlyList<string>? ConflictColumns = null,  // Columns that define uniqueness
    IReadOnlyList<UpsertUpdateClause>? UpdateClauses = null  // Values to update on conflict
)
{
    public void Validate()
    {
        if (UpdateClauses == null || UpdateClauses.Count == 0)
            throw new ArgumentException("At least one update clause must be specified for UPSERT", nameof(UpdateClauses));
    }
}

/// <summary>
/// Represents an UPSERT statement (INSERT ... ON CONFLICT / INSERT ... ON DUPLICATE KEY).
/// This is a combined INSERT/UPDATE operation that inserts a row if it doesn't exist,
/// or updates it if a conflict is detected (based on primary key or unique constraints).
/// 
/// Example SQL Server (MERGE):
///   MERGE INTO Users AS target
///   USING (SELECT @Id, @Name, @Email) AS source (Id, Name, Email)
///   WHEN NOT MATCHED THEN INSERT VALUES (...)
///   WHEN MATCHED THEN UPDATE SET Name = source.Name, Email = source.Email;
///
/// Example PostgreSQL:
///   INSERT INTO Users (Id, Name, Email) VALUES (@p1, @p2, @p3)
///   ON CONFLICT (Id) DO UPDATE SET Name = @p2, Email = @p3;
///
/// Example MySQL:
///   INSERT INTO Users (Id, Name, Email) VALUES (@p1, @p2, @p3)
///   ON DUPLICATE KEY UPDATE Name = @p2, Email = @p3;
/// </summary>
public sealed record UpsertStatement(
    TableReference Table,
    IReadOnlyList<Column> Columns,
    IReadOnlyList<object?>? Values = null,  // Single row values
    UpsertConflictClause? ConflictClause = null) : QueryNode
{
    public void Validate()
    {
        if (Table == null)
            throw new ArgumentNullException(nameof(Table));
        if (Columns == null || Columns.Count == 0)
            throw new ArgumentException("At least one column must be specified", nameof(Columns));
        if (Values == null || Values.Count == 0)
            throw new ArgumentException("Values must be provided for UPSERT", nameof(Values));
        if (Values.Count != Columns.Count)
            throw new ArgumentException("Column count must match value count", nameof(Values));
        
        ConflictClause?.Validate();
    }
}
