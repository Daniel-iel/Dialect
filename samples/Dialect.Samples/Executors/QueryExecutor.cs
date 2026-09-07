namespace Dialect.Samples.Executors;

/// <summary>
/// Represents the result of a query execution.
/// </summary>
public record ExecutionResult(
    List<dynamic> Rows,
    long ExecutionTimeMs,
    int RowCount,
    string? Error = null
);

/// <summary>
/// Abstract base class for dialect-specific query executors.
/// </summary>
public abstract class QueryExecutor
{
    protected readonly string _connectionString;

    protected QueryExecutor(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Execute a SELECT query and return results.
    /// </summary>
    public abstract Task<ExecutionResult> ExecuteQueryAsync(string sql, Dictionary<string, object?> parameters);

    /// <summary>
    /// Execute an INSERT, UPDATE, or DELETE query.
    /// </summary>
    public abstract Task<ExecutionResult> ExecuteNonQueryAsync(string sql, Dictionary<string, object?> parameters);

    /// <summary>
    /// Test the connection to the database.
    /// </summary>
    public abstract Task<bool> TestConnectionAsync();

    /// <summary>
    /// Get the dialect name for this executor.
    /// </summary>
    public abstract string GetDialectName();

    /// <summary>
    /// Convert parameter names from dialect-agnostic format to dialect-specific format.
    /// </summary>
    protected abstract Dictionary<string, object?> ConvertParameters(Dictionary<string, object?> parameters);
}
