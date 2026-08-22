namespace Dialect.Core.Versioning;

/// <summary>
/// Represents a database version with major, minor, and patch numbers.
/// </summary>
public record DatabaseVersion(int Major, int Minor, int Patch = 0)
{
    /// <summary>
    /// Parses a version string like "2019", "13.2", or "8.0.15" into DatabaseVersion.
    /// </summary>
    public static DatabaseVersion Parse(string version)
    {
        var parts = version.Split('.');
        if (!int.TryParse(parts[0], out var major))
            throw new ArgumentException($"Invalid version format: {version}");

        int minor = 0, patch = 0;
        if (parts.Length > 1 && !int.TryParse(parts[1], out minor))
            throw new ArgumentException($"Invalid version format: {version}");

        if (parts.Length > 2 && !int.TryParse(parts[2], out patch))
            throw new ArgumentException($"Invalid version format: {version}");

        return new DatabaseVersion(major, minor, patch);
    }

    /// <summary>
    /// Returns version as semantic string (e.g., "2019.0.0" or "13.2.0").
    /// </summary>
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Compares two versions. Returns -1 if this &lt; other, 0 if equal, 1 if this &gt; other.
    /// </summary>
    public int CompareTo(DatabaseVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    /// <summary>
    /// Checks if this version is greater than or equal to another.
    /// </summary>
    public bool IsAtLeast(DatabaseVersion minimumVersion) => CompareTo(minimumVersion) >= 0;
}
