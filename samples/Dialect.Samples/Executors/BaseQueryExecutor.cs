namespace Dialect.Samples.Executors;

using System.Data;
using System.Data.Common;
using System.Diagnostics;

/// <summary>
/// Base template for QueryExecutor implementations across all database dialects.
/// Consolidates common connection management, parameter handling, and result parsing logic.
/// 
/// Template Method Pattern:
/// - ExecuteQueryAsync() orchestrates the flow (open connection, execute, read results)
/// - Abstract methods allow dialect-specific implementation:
///   - CreateConnection() - Create dialect-specific DbConnection
///   - AddParameterToCommand() - Add parameters with dialect-specific prefix
/// - Result parsing is unified across all dialects
/// </summary>
public abstract class BaseQueryExecutor : QueryExecutor
{
    /// <summary>Command timeout in seconds for all database operations</summary>
    protected const int CommandTimeoutSeconds = 30;

    protected BaseQueryExecutor(string connectionString) : base(connectionString)
    {
    }

    /// <summary>
    /// Create a dialect-specific database connection.
    /// Must be implemented by subclasses (SqlConnection, NpgsqlConnection, MySqlConnection).
    /// </summary>
    protected abstract DbConnection CreateConnection();

    /// <summary>
    /// Add a parameter to the database command with dialect-specific handling.
    /// Subclasses implement to handle parameter prefix conventions (@, :, etc.).
    /// </summary>
    /// <param name="command">The DbCommand to add parameter to</param>
    /// <param name="paramName">Parameter name (may include prefix)</param>
    /// <param name="paramValue">Parameter value (null is converted to DBNull.Value)</param>
    protected abstract void AddParameterToCommand(DbCommand command, string paramName, object? paramValue);

    /// <summary>
    /// Template method for executing SELECT queries.
    /// Orchestrates: connection → command setup → parameter binding → result reading → cleanup.
    /// </summary>
    public override async Task<ExecutionResult> ExecuteQueryAsync(string sql, Dictionary<string, object?> parameters)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var rows = new List<dynamic>();
            var convertedParams = ConvertParameters(parameters);

            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.CommandTimeout = CommandTimeoutSeconds;

                    // Add all parameters using dialect-specific handler
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        AddParameterToCommand(command, paramName, paramValue);
                    }

                    // Execute and parse results
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        rows = await ParseResultsAsync(reader);
                    }
                }
            }

            sw.Stop();
            return new ExecutionResult(rows, sw.ElapsedMilliseconds, rows.Count);
        }
        catch (Exception ex)
        {
            return new ExecutionResult(new List<dynamic>(), 0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Template method for executing INSERT/UPDATE/DELETE commands.
    /// Returns affected row count instead of result set.
    /// </summary>
    public override async Task<ExecutionResult> ExecuteNonQueryAsync(string sql, Dictionary<string, object?> parameters)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var convertedParams = ConvertParameters(parameters);

            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.CommandTimeout = CommandTimeoutSeconds;

                    // Add all parameters using dialect-specific handler
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        AddParameterToCommand(command, paramName, paramValue);
                    }

                    int affectedRows = await command.ExecuteNonQueryAsync();
                    sw.Stop();

                    return new ExecutionResult(new List<dynamic>(), sw.ElapsedMilliseconds, affectedRows);
                }
            }
        }
        catch (Exception ex)
        {
            return new ExecutionResult(new List<dynamic>(), 0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Template method for testing database connection.
    /// Generic implementation works for all dialects.
    /// </summary>
    public override async Task<bool> TestConnectionAsync()
    {
        try
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse result set into a list of dynamic objects.
    /// Common implementation across all dialects (result reading is database-agnostic).
    /// </summary>
    /// <param name="reader">DataReader with result rows</param>
    /// <returns>List of dictionaries (each row) cast to dynamic</returns>
    protected virtual async Task<List<dynamic>> ParseResultsAsync(DbDataReader reader)
    {
        var rows = new List<dynamic>();

        // Get column names upfront
        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        // Read all rows asynchronously
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }
}
