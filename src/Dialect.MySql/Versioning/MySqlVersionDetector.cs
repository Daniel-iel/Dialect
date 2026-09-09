namespace Dialect.MySql.Versioning;

using Dialect.Core.Dialects;
using Dialect.Core.Versioning;

/// <summary>
/// MySQL version detector and capability provider.
/// Supports MySQL 8.0+ (version 8.0+).
/// </summary>
public class MySqlVersionDetector : VersionDetector
{
    private DatabaseVersion? _detectedVersion;

    public override DatabaseVersion DetectedVersion =>
        _detectedVersion ?? new DatabaseVersion(8, 0, 0);

    public override VersionCapabilities GetCapabilities(DatabaseVersion version)
    {
        // MySQL 8.0+ supports most features
        if (version.IsAtLeast(new DatabaseVersion(8, 0, 0)))
        {
            return new VersionCapabilities(
                SupportsWindowFunctions: true,
                SupportsCTEs: true,          // WITH clause
                SupportsUpsert: true,        // ON DUPLICATE KEY UPDATE
                SupportsJsonFunctions: true,
                SupportsFullTextSearch: true,
                SupportsPartitioning: true,
                SupportsGeneratedColumns: true,      // GENERATED ALWAYS AS
                SupportsCommonTableExpressions: true,
                SupportsPartialIndexes: false,       // Not supported in MySQL
                SupportsRecursiveCTEs: true          // Since MySQL 8.0.1
            );
        }

        // MySQL 5.7: Missing modern features
        if (version.IsAtLeast(new DatabaseVersion(5, 7, 0)))
        {
            return new VersionCapabilities(
                SupportsWindowFunctions: false,      // Added in 8.0
                SupportsCTEs: false,                 // Added in 8.0
                SupportsUpsert: true,                // INSERT ... ON DUPLICATE KEY UPDATE
                SupportsJsonFunctions: true,
                SupportsFullTextSearch: true,
                SupportsPartitioning: true,
                SupportsGeneratedColumns: true,
                SupportsCommonTableExpressions: false,
                SupportsPartialIndexes: false,
                SupportsRecursiveCTEs: false
            );
        }

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
            SupportsPartialIndexes: false,
            SupportsRecursiveCTEs: true
        );
    }

    public override DatabaseVersion? TryDetectVersion(ISqlDialect dialect)
    {
        // MVP: In production, would query VERSION() or SELECT @@version
        // SELECT @@version -> "8.0.23-0ubuntu0.20.04.1"
        _detectedVersion = new DatabaseVersion(8, 0, 0);
        return _detectedVersion;
    }
}
