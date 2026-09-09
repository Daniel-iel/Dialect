namespace Dialect.Core.Connection;

using Dialect.Core.Versioning;

/// <summary>
/// Detects database version by connecting to the database and querying version info.
/// Works with any IDbConnectionProvider implementation.
/// </summary>
public class QueryVersionDetector
{
    private readonly IDbConnectionProvider _provider;

    public QueryVersionDetector(IDbConnectionProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Detects the database version by executing a version query.
    /// </summary>
    /// <param name="connectionString">Connection string to database.</param>
    /// <returns>Detected DatabaseVersion or null if detection fails.</returns>
    /// <remarks>
    /// This method opens a connection, executes the version query, parses the result,
    /// and closes the connection. It does not perform connection pooling.
    /// </remarks>
    public DatabaseVersion? DetectVersion(string connectionString)
    {
        if (!_provider.ValidateConnectionString(connectionString))
            return null;

        object? connection = null;
        try
        {
            connection = _provider.OpenConnection(connectionString);
            var versionQuery = _provider.GetVersionQuery();
            var versionString = _provider.ExecuteScalar(connection, versionQuery);

            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            return _provider.ParseVersion(versionString);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (connection != null)
            {
                try
                {
                    _provider.CloseConnection(connection);
                }
                catch
                {
                    // Suppress close errors
                }
            }
        }
    }

    /// <summary>
    /// Detects version and gets capabilities for the database.
    /// </summary>
    /// <param name="connectionString">Connection string to database.</param>
    /// <param name="versionDetector">VersionDetector to get capabilities.</param>
    /// <returns>VersionCapabilities for the detected version or null if detection fails.</returns>
    public VersionCapabilities? DetectCapabilities(
        string connectionString,
        VersionDetector versionDetector)
    {
        if (versionDetector == null)
            throw new ArgumentNullException(nameof(versionDetector));

        var version = DetectVersion(connectionString);
        if (version == null)
            return null;

        return versionDetector.GetCapabilities(version);
    }
}
