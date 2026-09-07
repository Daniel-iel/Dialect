namespace Dialect.Samples.Services;

/// <summary>
/// Configuration for database connections used during batch execution.
/// </summary>
public class DatabaseConfiguration
{
    public string? SqlServerConnectionString { get; set; }
    public string? PostgreSqlConnectionString { get; set; }
    public string? MySqlConnectionString { get; set; }

    /// <summary>
    /// Load configuration from environment variables or appsettings.
    /// </summary>
    public static DatabaseConfiguration LoadFromEnvironment()
    {
        return new DatabaseConfiguration
        {
            SqlServerConnectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONN") 
                ?? "Server=localhost;Database=DialectSamples;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;",
            PostgreSqlConnectionString = Environment.GetEnvironmentVariable("POSTGRESQL_CONN")
                ?? "Host=localhost;Database=dialect_samples;Username=postgres;Password=postgres;Port=5432;",
            MySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONN")
                ?? "Server=localhost;Database=dialect_samples;User Id=root;Password=root;Port=3306;"
        };
    }

    /// <summary>
    /// Create a query executor for a specific dialect.
    /// </summary>
    public QueryExecutor? CreateExecutor(string dialectName)
    {
        return dialectName switch
        {
            "SQL Server" => new SqlServerExecutor(SqlServerConnectionString ?? ""),
            "PostgreSQL" => new PostgreSqlExecutor(PostgreSqlConnectionString ?? ""),
            "MySQL" => new MySqlExecutor(MySqlConnectionString ?? ""),
            _ => null
        };
    }

    /// <summary>
    /// Get all available executors.
    /// </summary>
    public Dictionary<string, QueryExecutor> GetAllExecutors()
    {
        var executors = new Dictionary<string, QueryExecutor>();

        if (!string.IsNullOrEmpty(SqlServerConnectionString))
        {
            executors["SQL Server"] = new SqlServerExecutor(SqlServerConnectionString);
        }

        if (!string.IsNullOrEmpty(PostgreSqlConnectionString))
        {
            executors["PostgreSQL"] = new PostgreSqlExecutor(PostgreSqlConnectionString);
        }

        if (!string.IsNullOrEmpty(MySqlConnectionString))
        {
            executors["MySQL"] = new MySqlExecutor(MySqlConnectionString);
        }

        return executors;
    }

    /// <summary>
    /// Test all configured connections.
    /// </summary>
    public async Task<Dictionary<string, bool>> TestAllConnectionsAsync()
    {
        var results = new Dictionary<string, bool>();
        var executors = GetAllExecutors();

        foreach (var (dialectName, executor) in executors)
        {
            results[dialectName] = await executor.TestConnectionAsync();
        }

        return results;
    }
}
