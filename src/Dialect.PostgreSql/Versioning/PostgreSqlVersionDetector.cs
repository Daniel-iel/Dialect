namespace Dialect.PostgreSql.Versioning;

using Dialect.Core.Dialects;
using Dialect.Core.Versioning;

/// <summary>
/// PostgreSQL version detector and capability provider.
/// Supports PostgreSQL 13+ (version 13.0+).
/// </summary>
public class PostgreSqlVersionDetector : VersionDetector
{
    private DatabaseVersion? _detectedVersion;

    public override DatabaseVersion DetectedVersion =>
        _detectedVersion ?? new DatabaseVersion(13, 0, 0);

    public override VersionCapabilities GetCapabilities(DatabaseVersion version)
    {
        // PostgreSQL 13+ supports all modern features
        if (version.IsAtLeast(new DatabaseVersion(13, 0, 0)))
        {
            return new VersionCapabilities(
                SupportsWindowFunctions: true,
                SupportsCTEs: true,
                SupportsUpsert: true,        // ON CONFLICT
                SupportsJsonFunctions: true,
                SupportsFullTextSearch: true,
                SupportsPartitioning: true,
                SupportsGeneratedColumns: true,  // GENERATED ALWAYS AS
                SupportsCommonTableExpressions: true,
                SupportsPartialIndexes: true,    // WHERE clause in indexes
                SupportsRecursiveCTEs: true
            );
        }

        // PostgreSQL 10-12: Missing some features
        if (version.IsAtLeast(new DatabaseVersion(10, 0, 0)))
        {
            return new VersionCapabilities(
                SupportsWindowFunctions: true,
                SupportsCTEs: true,
                SupportsUpsert: true,
                SupportsJsonFunctions: true,
                SupportsFullTextSearch: true,
                SupportsPartitioning: true,
                SupportsGeneratedColumns: false,  // Added in 12
                SupportsCommonTableExpressions: true,
                SupportsPartialIndexes: true,
                SupportsRecursiveCTEs: true
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
            SupportsPartialIndexes: true,
            SupportsRecursiveCTEs: true
        );
    }

    public override DatabaseVersion? TryDetectVersion(ISqlDialect dialect)
    {
        // MVP: In production, would query version() function
        // SELECT version() -> "PostgreSQL 13.2 (Debian 13.2-1.pgdg100+1) on x86_64-pc-linux-gnu"
        _detectedVersion = new DatabaseVersion(13, 0, 0);
        return _detectedVersion;
    }
}
