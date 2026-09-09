namespace Dialect.Cli.ErrorHandling;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a diagnostic error or warning with context information.
/// Used to provide actionable feedback to users with file, line, and column information.
/// </summary>
public sealed class SqlDiagnostic
{
    /// <summary>
    /// Severity level of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// Diagnostic code (e.g., "UNSUPPORTED_MERGE", "DYNAMIC_SQL_DETECTED").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// File path where the issue was detected.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Line number in the source file (1-indexed).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Column number in the source line (1-indexed).
    /// </summary>
    public int? ColumnNumber { get; set; }

    /// <summary>
    /// SQL snippet that caused the issue.
    /// </summary>
    public string? SqlSnippet { get; set; }

    /// <summary>
    /// Suggested fix or workaround.
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// Related documentation URL or resource.
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Additional context data (e.g., unsupported feature name).
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    public override string ToString()
    {
        var parts = new List<string>();

        var prefix = Severity switch
        {
            DiagnosticSeverity.Error => "❌ ERROR",
            DiagnosticSeverity.Warning => "⚠️  WARNING",
            DiagnosticSeverity.Info => "ℹ️  INFO",
            _ => "⚡ DIAGNOSTIC"
        };

        parts.Add(prefix);

        if (!string.IsNullOrEmpty(Code))
            parts.Add($"[{Code}]");

        var location = FilePath ?? "";
        if (LineNumber.HasValue)
            location += $":{LineNumber}";
        if (ColumnNumber.HasValue)
            location += $":{ColumnNumber}";

        if (!string.IsNullOrEmpty(location))
            parts.Add(location);

        return $"{string.Join(" ", parts)} {Message}";
    }
}

/// <summary>
/// Severity level for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Service for creating and managing SQL transformation diagnostics.
/// Provides context-aware error and warning messages with actionable suggestions.
/// </summary>
public sealed class DiagnosticService
{
    private readonly List<SqlDiagnostic> _diagnostics = new();

    public IReadOnlyList<SqlDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    /// <summary>
    /// Gets all errors.
    /// </summary>
    public IReadOnlyList<SqlDiagnostic> Errors => _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    /// <summary>
    /// Gets all warnings.
    /// </summary>
    public IReadOnlyList<SqlDiagnostic> Warnings => _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

    /// <summary>
    /// Gets all informational messages.
    /// </summary>
    public IReadOnlyList<SqlDiagnostic> Infos => _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info).ToList();

    /// <summary>
    /// Adds an error diagnostic.
    /// </summary>
    public void AddError(string code, string message, string? filePath = null, int? line = null, int? column = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Code = code,
            Message = message,
            FilePath = filePath,
            LineNumber = line,
            ColumnNumber = column
        });
    }

    /// <summary>
    /// Adds a warning diagnostic with optional suggestion.
    /// </summary>
    public void AddWarning(string code, string message, string? suggestion = null, string? filePath = null, int? line = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Code = code,
            Message = message,
            Suggestion = suggestion,
            FilePath = filePath,
            LineNumber = line
        });
    }

    /// <summary>
    /// Adds an info diagnostic.
    /// </summary>
    public void AddInfo(string code, string message, string? filePath = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Info,
            Code = code,
            Message = message,
            FilePath = filePath
        });
    }

    /// <summary>
    /// Adds a diagnostic for unsupported SQL features.
    /// </summary>
    public void AddUnsupportedFeature(string feature, string message, string? filePath = null, int? line = null, string? suggestion = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Code = "UNSUPPORTED_FEATURE",
            Message = $"{feature} is not fully supported: {message}",
            FilePath = filePath,
            LineNumber = line,
            Suggestion = suggestion,
            Context = new() { { "Feature", feature } }
        });
    }

    /// <summary>
    /// Adds a diagnostic for dynamic SQL detection.
    /// </summary>
    public void AddDynamicSqlDetected(string filePath, int? line = null, string? context = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Code = "DYNAMIC_SQL_DETECTED",
            Message = "Dynamic SQL (string interpolation/concatenation) cannot be automatically transformed",
            FilePath = filePath,
            LineNumber = line,
            Suggestion = "Manually refactor to use parameterized queries or stored procedures",
            Context = context != null ? new() { { "Context", context } } : new()
        });
    }

    /// <summary>
    /// Adds a diagnostic for parsing failure.
    /// </summary>
    public void AddParsingFailure(string sql, string filePath, int? line = null, string? parseError = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Code = "PARSE_FAILED",
            Message = $"Failed to parse SQL statement",
            FilePath = filePath,
            LineNumber = line,
            SqlSnippet = sql.Length > 80 ? sql.Substring(0, 80) + "..." : sql,
            Suggestion = parseError ?? "Check SQL syntax and ensure it follows standard SQL grammar",
            Context = new() { { "ParseError", parseError ?? "Unknown" } }
        });
    }

    /// <summary>
    /// Adds a diagnostic for transformation failure.
    /// </summary>
    public void AddTransformationFailure(string dialect, string reason, string? filePath = null, int? line = null)
    {
        _diagnostics.Add(new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Code = "TRANSFORMATION_FAILED",
            Message = $"Failed to transform SQL to {dialect}: {reason}",
            FilePath = filePath,
            LineNumber = line,
            Suggestion = "Review the SQL statement for compatibility with the target database.",
            Context = new() { { "TargetDialect", dialect } }
        });
    }

    /// <summary>
    /// Clears all diagnostics.
    /// </summary>
    public void Clear()
    {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Returns true if there are any errors.
    /// </summary>
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Returns true if there are any warnings or errors.
    /// </summary>
    public bool HasIssues => _diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Warning);

    /// <summary>
    /// Gets a formatted summary of all diagnostics.
    /// </summary>
    public string GetSummary()
    {
        if (_diagnostics.Count == 0)
            return "✅ No issues detected";

        var errorCount = Errors.Count;
        var warningCount = Warnings.Count;
        var infoCount = Infos.Count;

        var parts = new List<string>();
        if (errorCount > 0)
            parts.Add($"❌ {errorCount} error(s)");
        if (warningCount > 0)
            parts.Add($"⚠️  {warningCount} warning(s)");
        if (infoCount > 0)
            parts.Add($"ℹ️  {infoCount} info(s)");

        return string.Join(" | ", parts);
    }
}
