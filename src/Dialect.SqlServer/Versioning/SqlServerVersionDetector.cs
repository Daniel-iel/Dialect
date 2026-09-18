namespace Dialect.SqlServer.Versioning;

using Dialect.Core.Dialects;
using Dialect.Core.Versioning;

/// <summary>
/// SQL Server version detector and capability provider.
/// Supports SQL Server 2019+ (version 15.0+).
/// </summary>
public class SqlServerVersionDetector : VersionDetector
{
    private DatabaseVersion? _detectedVersion;

    public override DatabaseVersion DetectedVersion =>
        _detectedVersion ?? new DatabaseVersion(2019, 0, 0);

    public override VersionCapabilities GetCapabilities(DatabaseVersion version)
    {
        // SQL Server 2019 (15.0) and later support all features
        if (version.IsAtLeast(new DatabaseVersion(2019, 0, 0)))
        {
            return new VersionCapabilities(
                SupportsWindowFunctions: true,
                SupportsCTEs: true,
                SupportsUpsert: true,
                SupportsJsonFunctions: true,
                SupportsFullTextSearch: true,
                SupportsPartitioning: true,
                SupportsGeneratedColumns: true,
                SupportsCommonTableExpressions: true,
                SupportsPartialIndexes: true,  // Filtered indexes in SQL Server
                SupportsRecursiveCTEs: true
            );
        }

        // Default to permissive capabilities
        return GetDefaultCapabilities();
    }

    public override VersionCapabilities GetDefaultCapabilities()
    {
        return new VersionCapabilities(
            SupportsWindowFunctions: true,
            SupportsCTEs: true,
            SupportsUpsert: true,
            SupportsJsonFunctions: true,
            SupportsFullTextSearch: true,
            SupportsPartitioning: true,
            SupportsGeneratedColumns: true,
            SupportsCommonTableExpressions: true,
            SupportsPartialIndexes: true,
            SupportsRecursiveCTEs: true
        );
    }

    public override DatabaseVersion? TryDetectVersion(ISqlDialect dialect)
    {
        // MVP: In production, would query @@VERSION system variable
        // SELECT @@VERSION -> "Microsoft SQL Server 2019 (RTM-CU14) (KB4484710) ..."
        _detectedVersion = new DatabaseVersion(2019, 0, 0);
        return _detectedVersion;
    }
}
