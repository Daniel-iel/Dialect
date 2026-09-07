namespace Dialect.Samples.Utilities;

/// <summary>
/// Database connection strings for all supported databases.
/// </summary>
public static class DatabaseConnections
{
    /// <summary>
    /// SQL Server connection string (Docker default).
    /// </summary>
    public static string SqlServerConnectionString =>
        "Server=localhost,1433;Database=DialectSamples;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true;";

    /// <summary>
    /// PostgreSQL connection string (Docker default).
    /// </summary>
    public static string PostgreSqlConnectionString =>
        "Host=localhost;Port=5432;Database=dialect_samples;Username=postgres;Password=postgres;";

    /// <summary>
    /// MySQL connection string (Docker default).
    /// </summary>
    public static string MySqlConnectionString =>
        "Server=localhost;Port=3306;Database=dialect_samples;Uid=root;Pwd=root;";

}
