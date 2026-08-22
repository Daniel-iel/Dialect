namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Entry point for building SQL queries using a fluent API.
/// Thread-confined: never share a builder across threads before calling Build().
/// </summary>
public sealed class SqlBuilder
{
    /// <summary>
    /// Starts building a SELECT statement.
    /// </summary>
    public static SelectBuilder Select(params string[] columns)
    {
        if (columns == null || columns.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columns));
        
        var cols = columns.Select(c => new Column(c)).ToList();
        return new SelectBuilder(cols);
    }
    
    /// <summary>
    /// Starts building a SELECT statement with explicit Column objects.
    /// </summary>
    public static SelectBuilder Select(params Column[] columns)
    {
        if (columns == null || columns.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columns));
        
        return new SelectBuilder(columns.ToList());
    }
    
    /// <summary>
    /// Starts building an INSERT statement.
    /// </summary>
    public static InsertBuilder Insert()
    {
        return new InsertBuilder();
    }
    
    /// <summary>
    /// Starts building an UPDATE statement.
    /// </summary>
    public static UpdateBuilder Update()
    {
        return new UpdateBuilder();
    }
    
    /// <summary>
    /// Starts building a DELETE statement.
    /// </summary>
    public static DeleteBuilder Delete()
    {
        return new DeleteBuilder();
    }
    
    /// <summary>
    /// Starts building a routine call (procedure or function).
    /// </summary>
    public static RoutineCallBuilder Routine(string name, RoutineKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Routine name cannot be empty", nameof(name));
        
        return new RoutineCallBuilder(name, kind);
    }
    
    /// <summary>
    /// Starts building an UPSERT statement (INSERT ... ON CONFLICT / ON DUPLICATE KEY).
    /// </summary>
    public static UpsertBuilder Upsert(string tableName, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        
        return new UpsertBuilder(tableName, schema);
    }
}
