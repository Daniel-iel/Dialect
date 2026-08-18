namespace Dialect.Core.Versioning;

using Dialect.Core.Dialects;

/// <summary>
/// Detects database version and determines supported capabilities.
/// </summary>
public abstract class VersionDetector
{
    /// <summary>
    /// Gets the detected database version.
    /// </summary>
    public abstract DatabaseVersion DetectedVersion { get; }

    /// <summary>
    /// Gets capabilities for a specific database version.
    /// </summary>
    public abstract VersionCapabilities GetCapabilities(DatabaseVersion version);

    /// <summary>
    /// Gets default/fallback capabilities when version cannot be detected.
    /// </summary>
    public abstract VersionCapabilities GetDefaultCapabilities();

    /// <summary>
    /// Detects version from database connection string or metadata.
    /// Returns null if detection fails.
    /// </summary>
    public abstract DatabaseVersion? TryDetectVersion(ISqlDialect dialect);
}
