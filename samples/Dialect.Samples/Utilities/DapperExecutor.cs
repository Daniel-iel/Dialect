using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace Dialect.Samples.Utilities;

/// <summary>
/// DapperExecutor handles database connections and query execution using Dapper
/// across SQL Server, PostgreSQL, and MySQL databases.
/// Note: Dapper automatically performs case-insensitive property matching,
/// so snake_case columns map to PascalCase properties automatically.
/// </summary>
public static class DapperExecutor
{
    /// <summary>
    /// Gets a database connection for the specified dialect.
    /// </summary>
    /// <param name="dialectName">The dialect name (e.g., "SQL Server", "PostgreSQL", "MySQL")</param>
    /// <returns>A DbConnection instance</returns>
    /// <exception cref="ArgumentException">Thrown if the dialect is not supported</exception>
    public static DbConnection GetConnection(string dialectName)
    {
        return dialectName.ToLower() switch
        {
            "sql server" => new SqlConnection(DatabaseConnections.SqlServerConnectionString),
            "postgresql" => new NpgsqlConnection(DatabaseConnections.PostgreSqlConnectionString),
            "mysql" => new MySqlConnection(DatabaseConnections.MySqlConnectionString),
            _ => throw new ArgumentException($"Unsupported dialect: {dialectName}", nameof(dialectName))
        };
    }

    /// <summary>
    /// Executes a query and returns results of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to map results to</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query string</param>
    /// <param name="parameters">Dictionary of parameters (key: parameter name, value: parameter value)</param>
    /// <returns>A list of results mapped to type T</returns>
    public static async Task<List<T>> QueryAsync<T>(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParams = ConvertParametersToDynamic(parameters);
        var results = await connection.QueryAsync<T>(sql, dynamicParams);
        return results.ToList();
    }

    /// <summary>
    /// Executes a query without type mapping, returning dynamic objects.
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query string</param>
    /// <param name="parameters">Dictionary of parameters</param>
    /// <returns>A list of dynamic results</returns>
    public static async Task<List<dynamic>> QueryDynamicAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParams = ConvertParametersToDynamic(parameters);
        var results = await connection.QueryAsync(sql, dynamicParams);
        return results.Cast<dynamic>().ToList();
    }

    /// <summary>
    /// Executes a non-query command (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command string</param>
    /// <param name="parameters">Dictionary of parameters</param>
    /// <returns>The number of rows affected</returns>
    public static async Task<int> ExecuteAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParams = ConvertParametersToDynamic(parameters);
        return await connection.ExecuteAsync(sql, dynamicParams);
    }

    /// <summary>
    /// Executes a query that returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The type of the scalar value</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query string</param>
    /// <param name="parameters">Dictionary of parameters</param>
    /// <returns>The scalar value</returns>
    public static async Task<T?> QueryScalarAsync<T>(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParams = ConvertParametersToDynamic(parameters);
        return await connection.QueryFirstOrDefaultAsync<T>(sql, dynamicParams);
    }

    /// <summary>
    /// Converts a Dictionary of parameters to Dapper's DynamicParameters.
    /// This strips the parameter prefix (@ for SQL Server, : for PostgreSQL, ? for MySQL)
    /// to create clean parameter names.
    /// </summary>
    /// <param name="parameters">Dictionary of parameters with prefixes</param>
    /// <returns>DynamicParameters object ready for Dapper</returns>
    private static DynamicParameters ConvertParametersToDynamic(
        IReadOnlyDictionary<string, object?> parameters)
    {
        var dynamicParams = new DynamicParameters();

        if (parameters == null || parameters.Count == 0)
            return dynamicParams;

        foreach (var (key, value) in parameters)
        {
            // Remove common parameter prefixes if they exist
            var paramName = key.StartsWith("@")
                ? key[1..]
                : key.StartsWith(":")
                    ? key[1..]
                    : key;

            dynamicParams.Add(paramName, value);
        }

        return dynamicParams;
    }
}
