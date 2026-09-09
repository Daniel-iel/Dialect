namespace Dialect.MySql.Connection;

using System.Data;
using MySqlConnector;
using Dialect.Core.Connection;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.MySql;

/// <summary>
/// MySQL implementation of IDbConnectionProvider.
/// Handles MySQL connections and version detection.
/// </summary>
public class MySqlConnectionProvider : IDbConnectionProvider
{
    private readonly MySqlDialect _dialect;

    public ISqlDialect Dialect => _dialect;

    public MySqlConnectionProvider()
    {
        _dialect = new MySqlDialect();
    }

    public object OpenConnection(string connectionString)
    {
        if (!ValidateConnectionString(connectionString))
            throw new ArgumentException("Invalid MySQL connection string", nameof(connectionString));

        var connection = new MySqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void CloseConnection(object connection)
    {
        if (connection is MySqlConnection mysqlConnection)
        {
            mysqlConnection.Close();
            mysqlConnection.Dispose();
        }
    }

    public string ExecuteScalar(object connection, string query)
    {
        if (connection is not MySqlConnection mysqlConnection)
            throw new ArgumentException("Expected MySqlConnection", nameof(connection));

        using (var command = new MySqlCommand(query, mysqlConnection))
        {
            command.CommandTimeout = 10;
            var result = command.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }
    }

    public IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query)
    {
        if (connection is not MySqlConnection mysqlConnection)
            throw new ArgumentException("Expected MySqlConnection", nameof(connection));

        var results = new List<Dictionary<string, object>>();
        using (var command = new MySqlCommand(query, mysqlConnection))
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
        // MySQL: @@version or VERSION() function
        // Example: "8.0.23"
        return "SELECT @@version AS Version";
    }

    public DatabaseVersion? ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        // Parse MySQL version format
        // Examples:
        // "8.0.23"
        // "5.7.32"
        // "8.0.23-0ubuntu0.20.04.1" (Ubuntu variant)

        // Extract version numbers from format "X.Y.Z"
        var match = System.Text.RegularExpressions.Regex.Match(
            versionString,
            @"^(\d+)\.(\d+)\.(\d+)"
        );

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int major) &&
            int.TryParse(match.Groups[2].Value, out int minor) &&
            int.TryParse(match.Groups[3].Value, out int patch))
        {
            return new DatabaseVersion(major, minor, patch);
        }

        return null;
    }

    public bool ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            // Basic validation: must have server
            return !string.IsNullOrWhiteSpace(builder.Server);
        }
        catch
        {
            return false;
        }
    }
}
