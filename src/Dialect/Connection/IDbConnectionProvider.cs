namespace Dialect.Core.Connection;

using Dialect.Core.Dialects;
using Dialect.Core.Versioning;

/// <summary>
/// Abstraction for database connection management.
/// Implementations handle dialect-specific connection details.
/// </summary>
public interface IDbConnectionProvider
{
    /// <summary>
    /// Gets the SQL dialect this provider targets.
    /// </summary>
    ISqlDialect Dialect { get; }

    /// <summary>
    /// Opens a connection to the database.
    /// </summary>
    /// <param name="connectionString">Connection string for the database.</param>
    /// <returns>A database connection object.</returns>
    object OpenConnection(string connectionString);

    /// <summary>
    /// Closes a connection gracefully.
    /// </summary>
    /// <param name="connection">Connection object to close.</param>
    void CloseConnection(object connection);

    /// <summary>
    /// Executes a scalar query (returns single value).
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="query">SQL query to execute.</param>
    /// <returns>Scalar result (e.g., version string).</returns>
    string ExecuteScalar(object connection, string query);

    /// <summary>
    /// Executes a query that returns multiple rows.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="query">SQL query to execute.</param>
    /// <returns>List of row results (each row as string dictionary or tuple).</returns>
    IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query);

    /// <summary>
    /// Gets the query to detect database version for this dialect.
    /// </summary>
    /// <returns>SQL query that returns version string.</returns>
    string GetVersionQuery();

    /// <summary>
    /// Parses a raw version string into structured DatabaseVersion.
    /// </summary>
    /// <param name="versionString">Raw version string from database.</param>
    /// <returns>Parsed DatabaseVersion or null if parsing fails.</returns>
    DatabaseVersion? ParseVersion(string versionString);

    /// <summary>
    /// Validates that a connection string is valid for this dialect.
    /// </summary>
    /// <param name="connectionString">Connection string to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool ValidateConnectionString(string connectionString);
}
