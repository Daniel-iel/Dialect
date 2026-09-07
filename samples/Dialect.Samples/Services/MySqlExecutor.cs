using System.Diagnostics;
using MySqlConnector;

namespace Dialect.Samples.Services;

/// <summary>
/// MySQL specific query executor.
/// </summary>
public class MySqlExecutor : QueryExecutor
{
    public MySqlExecutor(string connectionString) : base(connectionString)
    {
    }

    public override string GetDialectName() => "MySQL";

    public override async Task<ExecutionResult> ExecuteQueryAsync(string sql, Dictionary<string, object?> parameters)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var rows = new List<dynamic>();
            var convertedParams = ConvertParameters(parameters);

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.CommandTimeout = 30;

                    // Add parameters
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        command.Parameters.AddWithValue(paramName, paramValue ?? DBNull.Value);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Get column names
                        var columnNames = Enumerable.Range(0, reader.FieldCount)
                            .Select(i => reader.GetName(i))
                            .ToList();

                        // Read all rows
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object?>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            rows.Add(row);
                        }
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

    public override async Task<ExecutionResult> ExecuteNonQueryAsync(string sql, Dictionary<string, object?> parameters)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var convertedParams = ConvertParameters(parameters);

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.CommandTimeout = 30;

                    // Add parameters
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        command.Parameters.AddWithValue(paramName, paramValue ?? DBNull.Value);
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

    public override async Task<bool> TestConnectionAsync()
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
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

    protected override Dictionary<string, object?> ConvertParameters(Dictionary<string, object?> parameters)
    {
        // MySQL parameters: MySqlCommand expects @p1, @p2 format or ? placeholders
        // We'll use the @pX format which is standard
        var converted = new Dictionary<string, object?>();
        foreach (var (key, value) in parameters)
        {
            var paramName = key.StartsWith("@") ? key : "@" + key;
            converted[paramName] = value;
        }
        return converted;
    }
}
