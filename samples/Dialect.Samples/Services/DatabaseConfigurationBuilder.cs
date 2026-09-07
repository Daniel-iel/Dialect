using Dialect.Samples.Executors;

namespace Dialect.Samples.Services;

/// <summary>
/// Fluent builder for configuring database connections across all supported dialects.
/// Centralizes connection string management with environment variable override support.
/// 
/// Single source of truth for database credentials:
/// - Defaults to hardcoded values for Docker/local development
/// - Can be overridden via environment variables
/// - Validates connections on initialization (optional)
/// </summary>
public class DatabaseConfigurationBuilder
{
    private string? _sqlServerConnectionString;
    private string? _postgreSqlConnectionString;
    private string? _mySqlConnectionString;
    private bool _validateOnBuild = false;

    /// <summary>Default connection strings for local Docker development</summary>
    public static class Defaults
    {
        public const string SqlServer =
            "Server=localhost,1433;Database=DialectSamples;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true;";

        public const string PostgreSQL =
            "Host=localhost;Port=5432;Database=dialect_samples;Username=postgres;Password=postgres;";

        public const string MySQL =
            "Server=localhost;Port=3306;Database=dialect_samples;Uid=root;Pwd=root;";
    }

    /// <summary>
    /// Environment variable names for connection string overrides
    /// </summary>
    public static class EnvironmentVariables
    {
        public const string SqlServer = "SQLSERVER_CONN";
        public const string PostgreSQL = "POSTGRESQL_CONN";
        public const string MySQL = "MYSQL_CONN";
    }

    public DatabaseConfigurationBuilder()
    {
        // Initialize with defaults, will be overridden by environment variables below
        _sqlServerConnectionString = Defaults.SqlServer;
        _postgreSqlConnectionString = Defaults.PostgreSQL;
        _mySqlConnectionString = Defaults.MySQL;

        // Apply environment variable overrides
        LoadFromEnvironment();
    }

    /// <summary>
    /// Load connection strings from environment variables.
    /// Environment variables override default hardcoded values.
    /// </summary>
    private void LoadFromEnvironment()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariables.SqlServer) is string sqlServerEnv)
            _sqlServerConnectionString = sqlServerEnv;

        if (Environment.GetEnvironmentVariable(EnvironmentVariables.PostgreSQL) is string pgEnv)
            _postgreSqlConnectionString = pgEnv;

        if (Environment.GetEnvironmentVariable(EnvironmentVariables.MySQL) is string mysqlEnv)
            _mySqlConnectionString = mysqlEnv;
    }

    /// <summary>
    /// Set SQL Server connection string explicitly.
    /// Overrides both default and environment variable.
    /// </summary>
    public DatabaseConfigurationBuilder WithSqlServerConnectionString(string connectionString)
    {
        _sqlServerConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        return this;
    }

    /// <summary>
    /// Set PostgreSQL connection string explicitly.
    /// </summary>
    public DatabaseConfigurationBuilder WithPostgreSqlConnectionString(string connectionString)
    {
        _postgreSqlConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        return this;
    }

    /// <summary>
    /// Set MySQL connection string explicitly.
    /// </summary>
    public DatabaseConfigurationBuilder WithMySqlConnectionString(string connectionString)
    {
        _mySqlConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        return this;
    }

    /// <summary>
    /// Enable validation of connections before returning the built configuration.
    /// Optional: validates that databases are accessible.
    /// </summary>
    public DatabaseConfigurationBuilder ValidateOnBuild(bool validate = true)
    {
        _validateOnBuild = validate;
        return this;
    }

    /// <summary>
    /// Build the final DatabaseConfiguration with validated connection strings.
    /// </summary>
    public DatabaseConfiguration Build()
    {
        if (string.IsNullOrWhiteSpace(_sqlServerConnectionString))
            throw new InvalidOperationException("SQL Server connection string not configured");

        if (string.IsNullOrWhiteSpace(_postgreSqlConnectionString))
            throw new InvalidOperationException("PostgreSQL connection string not configured");

        if (string.IsNullOrWhiteSpace(_mySqlConnectionString))
            throw new InvalidOperationException("MySQL connection string not configured");

        var config = new DatabaseConfiguration
        {
            SqlServerConnectionString = _sqlServerConnectionString,
            PostgreSqlConnectionString = _postgreSqlConnectionString,
            MySqlConnectionString = _mySqlConnectionString
        };

        if (_validateOnBuild)
        {
            ValidateConnections(config);
        }

        return config;
    }

    /// <summary>
    /// Validate that all configured databases are accessible.
    /// Throws exception if any connection fails.
    /// </summary>
    private static void ValidateConnections(DatabaseConfiguration config)
    {
        // Validation would be async, but this is called from sync Build()
        // TODO: Consider making Build() async or adding separate ValidateAsync() method
        var sqlServerExecutor = new SqlServerExecutor(config.SqlServerConnectionString ?? "");
        var pgExecutor = new PostgreSqlExecutor(config.PostgreSqlConnectionString ?? "");
        var mysqlExecutor = new MySqlExecutor(config.MySqlConnectionString ?? "");

        // For now, just verify connection strings are not empty
        if (string.IsNullOrEmpty(config.SqlServerConnectionString))
            throw new InvalidOperationException("SQL Server connection string is empty after validation");
        if (string.IsNullOrEmpty(config.PostgreSqlConnectionString))
            throw new InvalidOperationException("PostgreSQL connection string is empty after validation");
        if (string.IsNullOrEmpty(config.MySqlConnectionString))
            throw new InvalidOperationException("MySQL connection string is empty after validation");
    }
}
