namespace Dialect.PostgreSql.Connection;

using System.Data;
using Npgsql;
using Dialect.Core.Connection;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.PostgreSql;

/// <summary>
/// PostgreSQL implementation of IDbConnectionProvider.
/// Handles ANSI SQL connections and version detection.
/// </summary>
public class PostgreSqlConnectionProvider : IDbConnectionProvider
{
    private readonly PostgreSqlDialect _dialect;

    public ISqlDialect Dialect => _dialect;

    public PostgreSqlConnectionProvider()
    {
        _dialect = new PostgreSqlDialect();
    }

    public object OpenConnection(string connectionString)
    {
        if (!ValidateConnectionString(connectionString))
            throw new ArgumentException("Invalid PostgreSQL connection string", nameof(connectionString));

        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void CloseConnection(object connection)
    {
        if (connection is NpgsqlConnection npgsqlConnection)
        {
            npgsqlConnection.Close();
            npgsqlConnection.Dispose();
        }
    }

    public string ExecuteScalar(object connection, string query)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new ArgumentException("Expected NpgsqlConnection", nameof(connection));

        using (var command = new NpgsqlCommand(query, npgsqlConnection))
        {
            command.CommandTimeout = 10;
            var result = command.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }
    }

    public IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new ArgumentException("Expected NpgsqlConnection", nameof(connection));

        var results = new List<Dictionary<string, object>>();
        using (var command = new NpgsqlCommand(query, npgsqlConnection))
        {
            command.CommandTimeout = 10;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.GetValue(i);
                    }
                    results.Add(row);
                }
            }
        }
        return results;
    }

    public string GetVersionQuery()
    {
        // PostgreSQL: version() function returns detailed version info
        // Example: "PostgreSQL 13.2 (Debian 13.2-1.pgdg100+1) on x86_64-pc-linux-gnu..."
        return "SELECT version() AS Version";
    }

    public DatabaseVersion? ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        // Parse PostgreSQL version format
        // Examples:
        // "PostgreSQL 13.2 (Debian 13.2-1.pgdg100+1) on x86_64..."
        // "PostgreSQL 14.5 on x86_64-pc-linux-gnu..."
        
        // Extract version numbers from format "X.Y"
        var match = System.Text.RegularExpressions.Regex.Match(
            versionString,
            @"PostgreSQL\s+(\d+)\.(\d+)"
        );

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int major) &&
            int.TryParse(match.Groups[2].Value, out int minor))
        {
            return new DatabaseVersion(major, minor, 0);
        }

        return null;
    }

    public bool ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            // Basic validation: must have host
            return !string.IsNullOrWhiteSpace(builder.Host);
        }
        catch
        {
            return false;
        }
    }
}
