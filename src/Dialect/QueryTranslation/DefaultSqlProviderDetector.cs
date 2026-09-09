namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;

/// <summary>
/// Default implementation of ISqlProviderDetector using heuristic pattern matching
/// on connection string keywords.
/// </summary>
public sealed class DefaultSqlProviderDetector : ISqlProviderDetector
{
    /// <summary>
    /// Detects SQL provider from connection string using pattern matching.
    /// Order of detection: SqlServer → PostgreSql → MySql
    /// Returns null if no provider can be confidently detected.
    /// </summary>
    public SqlProvider? Detect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var connStr = connectionString.ToLowerInvariant();

        // MySQL detection patterns (check first - port=3306 is very specific)
        if (connStr.Contains("port=3306") ||
            connStr.Contains("mysql"))
        {
            return SqlProvider.MySql;
        }

        // PostgreSQL detection patterns
        if (connStr.Contains("host=") ||
            connStr.Contains("postgres") ||
            connStr.Contains(".rds.amazonaws.com"))
        {
            return SqlProvider.PostgreSql;
        }

        // SQL Server detection patterns (last - "server=" is generic)
        if (connStr.Contains("server=") ||
            connStr.Contains("data source=") ||
            connStr.Contains(".database.windows.net") ||
            connStr.Contains("sqlserver"))
        {
            return SqlProvider.SqlServer;
        }

        // Confidence not high enough
        return null;
    }
}
