namespace Dialect.Cli.Security;

using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Provides security validation and sanitization for file paths and inputs.
/// Prevents path traversal attacks and validates safe file operations.
/// </summary>
public sealed class SecurityValidator
{
    /// <summary>
    /// Validates that a path is within an allowed base directory.
    /// Prevents path traversal attacks (../ sequences).
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="baseDirectory">The allowed base directory.</param>
    /// <returns>True if path is safe and within baseDirectory.</returns>
    public static bool IsPathSafe(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(baseDirectory))
            return false;

        try
        {
            // Resolve full paths
            var fullPath = Path.GetFullPath(path);
            var fullBase = Path.GetFullPath(baseDirectory);

            // Ensure base directory ends with separator for proper comparison
            if (!fullBase.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullBase += Path.DirectorySeparatorChar;

            // Path must start with base directory
            if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                return false;

            // Additional check: no ".." sequences in the resolved path
            if (fullPath.Contains(".." + Path.DirectorySeparatorChar) ||
                fullPath.EndsWith(".."))
                return false;

            return true;
        }
        catch
        {
            // Any error in path resolution = unsafe
            return false;
        }
    }

    /// <summary>
    /// Validates a directory exists and is accessible.
    /// </summary>
    public static bool IsDirectoryAccessible(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            var info = new DirectoryInfo(directory);
            return info.Exists && (info.Attributes & FileAttributes.Hidden) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a file exists and is accessible.
    /// </summary>
    public static bool IsFileAccessible(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            var info = new FileInfo(filePath);
            return info.Exists && (info.Attributes & FileAttributes.Hidden) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes SQL input to remove potentially dangerous patterns.
    /// Note: Does NOT prevent SQL injection - use parameterized queries!
    /// This is for logging/display purposes only.
    /// </summary>
    public static string SanitizeSqlForLogging(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        // Remove potentially sensitive parts for logging
        var sanitized = sql;

        // Mask common credential patterns (simplified)
        var passwordPattern = @"password\s*=\s*['\x22][^\x22']*['\x22]";
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized, 
            passwordPattern, 
            m => "password = '***'",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var apiKeyPattern = @"api[_-]?key\s*[=:]\s*['\x22][^\x22']*['\x22]";
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            apiKeyPattern,
            m => "api_key = '***'",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Truncate if too long (prevent log flooding)
        if (sanitized.Length > 500)
            sanitized = sanitized.Substring(0, 497) + "...";

        return sanitized;
    }

    /// <summary>
    /// Validates file name is safe (no directory separators or path traversal).
    /// </summary>
    public static bool IsFileNameSafe(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Check for path separators
        if (fileName.Contains(Path.DirectorySeparatorChar.ToString()) ||
            fileName.Contains(Path.AltDirectorySeparatorChar.ToString()) ||
            fileName.Contains(".."))
            return false;

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in fileName)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates glob pattern is safe and doesn't escape the base directory.
    /// </summary>
    public static bool IsGlobPatternSafe(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // Glob patterns should not contain path traversal
        if (pattern.Contains(".."))
            return false;

        // Should not contain absolute paths
        if (Path.IsPathRooted(pattern))
            return false;

        return true;
    }

    /// <summary>
    /// Gets a list of safe glob patterns from a comma-separated list.
    /// Returns only valid patterns, logs any that are rejected.
    /// </summary>
    public static IReadOnlyList<string> GetSafeGlobPatterns(string patternsInput)
    {
        if (string.IsNullOrWhiteSpace(patternsInput))
            return Array.Empty<string>();

        var patterns = new List<string>();
        var parts = patternsInput.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed) && IsGlobPatternSafe(trimmed))
            {
                patterns.Add(trimmed);
            }
        }

        return patterns;
    }
}
