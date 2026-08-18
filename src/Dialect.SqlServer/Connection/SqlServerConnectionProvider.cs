namespace Dialect.SqlServer.Connection;

using System.Data;
using System.Data.SqlClient;
using Dialect.Core.Connection;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.SqlServer;

/// <summary>
/// SQL Server implementation of IDbConnectionProvider.
/// Handles T-SQL connections and version detection.
/// </summary>
public class SqlServerConnectionProvider : IDbConnectionProvider
{
    private readonly SqlServerDialect _dialect;

    public ISqlDialect Dialect => _dialect;

    public SqlServerConnectionProvider()
    {
        _dialect = new SqlServerDialect();
    }

    public object OpenConnection(string connectionString)
    {
        if (!ValidateConnectionString(connectionString))
            throw new ArgumentException("Invalid SQL Server connection string", nameof(connectionString));

        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void CloseConnection(object connection)
    {
        if (connection is SqlConnection sqlConnection)
        {
            sqlConnection.Close();
            sqlConnection.Dispose();
        }
    }

    public string ExecuteScalar(object connection, string query)
    {
        if (connection is not SqlConnection sqlConnection)
            throw new ArgumentException("Expected SqlConnection", nameof(connection));

        using (var command = new SqlCommand(query, sqlConnection))
        {
            command.CommandTimeout = 10;
            var result = command.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }
    }

    public IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query)
    {
        if (connection is not SqlConnection sqlConnection)
            throw new ArgumentException("Expected SqlConnection", nameof(connection));

        var results = new List<Dictionary<string, object>>();
        using (var command = new SqlCommand(query, sqlConnection))
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
        // SQL Server: @@VERSION contains version info
        // Example: "Microsoft SQL Server 2019 (RTM) - 15.0.2000.5 (X64) ... Build date: Sep 30 2019"
        return "SELECT @@VERSION AS Version";
    }

    public DatabaseVersion? ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        // Parse SQL Server version format
        // Examples:
        // "Microsoft SQL Server 2019 (RTM) - 15.0.2000.5"
        // "Microsoft SQL Server 2022 (RTM) - 16.0.1000"
        
        // Extract version numbers from format "XX.Y.ZZZZ"
        var match = System.Text.RegularExpressions.Regex.Match(
            versionString,
            @"(\d+)\.(\d+)\.(\d+)"
        );

        if (match.Success && 
            int.TryParse(match.Groups[1].Value, out int major) &&
            int.TryParse(match.Groups[2].Value, out int minor) &&
            int.TryParse(match.Groups[3].Value, out int patch))
        {
            // Map SQL Server internal version to marketing version
            // 15.x = 2019, 16.x = 2022, etc.
            int marketingVersion = major switch
            {
                15 => 2019,
                16 => 2022,
                _ => major // Fallback for unknown versions
            };

            return new DatabaseVersion(marketingVersion, minor, patch);
        }

        return null;
    }

    public bool ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            // Basic validation: must have server
            return !string.IsNullOrWhiteSpace(builder.DataSource);
        }
        catch
        {
            return false;
        }
    }
}
