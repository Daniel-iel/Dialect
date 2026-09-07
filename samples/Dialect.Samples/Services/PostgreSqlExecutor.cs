using System.Diagnostics;
using Npgsql;

namespace Dialect.Samples.Services;

/// <summary>
/// PostgreSQL specific query executor.
/// </summary>
public class PostgreSqlExecutor : QueryExecutor
{
    public PostgreSqlExecutor(string connectionString) : base(connectionString)
    {
    }

    public override string GetDialectName() => "PostgreSQL";

    public override async Task<ExecutionResult> ExecuteQueryAsync(string sql, Dictionary<string, object?> parameters)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var rows = new List<dynamic>();
            var convertedParams = ConvertParameters(parameters);

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.CommandTimeout = 30;

                    // Add parameters
                    int paramIndex = 1;
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        command.Parameters.AddWithValue(paramName, paramValue ?? DBNull.Value);
                        paramIndex++;
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

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.CommandTimeout = 30;

                    // Add parameters
                    int paramIndex = 1;
                    foreach (var (paramName, paramValue) in convertedParams)
                    {
                        command.Parameters.AddWithValue(paramName, paramValue ?? DBNull.Value);
                        paramIndex++;
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
            using (var connection = new NpgsqlConnection(_connectionString))
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
        // PostgreSQL parameters: convert from @p1, @p2 to $1, $2
        // However, NpgsqlCommand expects positional parameters
        // We need to keep them as @p1, @p2 format and let Npgsql handle conversion
        return parameters;
    }
}
