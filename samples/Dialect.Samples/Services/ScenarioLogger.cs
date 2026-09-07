namespace Dialect.Samples.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Dialect.Samples.Utilities;

/// <summary>
/// Structured logging for scenario execution.
/// Captures execution metrics and diagnostics for monitoring and debugging.
/// </summary>
public class ScenarioLogger
{
    private readonly List<ExecutionLogEntry> _logEntries;
    private readonly bool _enableConsoleOutput;

    /// <summary>
    /// Initializes the logger.
    /// </summary>
    /// <param name="enableConsoleOutput">Whether to print log entries to console immediately</param>
    public ScenarioLogger(bool enableConsoleOutput = false)
    {
        _logEntries = new List<ExecutionLogEntry>();
        _enableConsoleOutput = enableConsoleOutput;
    }

    /// <summary>
    /// Gets all logged entries.
    /// </summary>
    public IReadOnlyList<ExecutionLogEntry> Entries => _logEntries.AsReadOnly();

    /// <summary>
    /// Logs scenario execution start.
    /// </summary>
    public ExecutionLogEntry LogScenarioStart(string scenarioName, string description = "")
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Information,
            $"Scenario Started: {scenarioName}",
            new Dictionary<string, object?>
            {
                { "scenario_name", scenarioName },
                { "description", description }
            }
        );
        _logEntries.Add(entry);
        if (_enableConsoleOutput)
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] ℹ {entry.Message}");
        return entry;
    }

    /// <summary>
    /// Logs scenario execution completion with timing.
    /// </summary>
    public ExecutionLogEntry LogScenarioComplete(
        string scenarioName,
        long executionTimeMs,
        int resultCount = 0)
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Information,
            $"Scenario Completed: {scenarioName} ({executionTimeMs}ms, {resultCount} results)",
            new Dictionary<string, object?>
            {
                { "scenario_name", scenarioName },
                { "execution_time_ms", executionTimeMs },
                { "result_count", resultCount }
            }
        );
        _logEntries.Add(entry);
        if (_enableConsoleOutput)
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] ✓ {entry.Message}");
        return entry;
    }

    /// <summary>
    /// Logs scenario execution error.
    /// </summary>
    public ExecutionLogEntry LogScenarioError(
        string scenarioName,
        string errorMessage,
        string? errorDetails = null)
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Error,
            $"Scenario Failed: {scenarioName} - {errorMessage}",
            new Dictionary<string, object?>
            {
                { "scenario_name", scenarioName },
                { "error_message", errorMessage },
                { "error_details", errorDetails }
            }
        );
        _logEntries.Add(entry);
        if (_enableConsoleOutput)
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] ✗ {entry.Message}");
        return entry;
    }

    /// <summary>
    /// Logs compilation information.
    /// </summary>
    public ExecutionLogEntry LogCompilation(
        string scenarioName,
        string dialect,
        long compilationTimeMs)
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Debug,
            $"Compiled for {dialect}: {scenarioName} ({compilationTimeMs}ms)",
            new Dictionary<string, object?>
            {
                { "scenario_name", scenarioName },
                { "dialect", dialect },
                { "compilation_time_ms", compilationTimeMs }
            }
        );
        _logEntries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Logs query execution.
    /// </summary>
    public ExecutionLogEntry LogQueryExecution(
        string scenarioName,
        string dialect,
        long executionTimeMs,
        int rowsAffected = 0)
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Debug,
            $"Query executed on {dialect}: {executionTimeMs}ms, {rowsAffected} rows",
            new Dictionary<string, object?>
            {
                { "scenario_name", scenarioName },
                { "dialect", dialect },
                { "execution_time_ms", executionTimeMs },
                { "rows_affected", rowsAffected }
            }
        );
        _logEntries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public ExecutionLogEntry LogWarning(string message, Dictionary<string, object?>? metadata = null)
    {
        var entry = new ExecutionLogEntry(
            DateTime.UtcNow,
            LogLevel.Warning,
            message,
            metadata ?? new Dictionary<string, object?>()
        );
        _logEntries.Add(entry);
        if (_enableConsoleOutput)
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] ⚠ {message}");
        return entry;
    }

    /// <summary>
    /// Gets a formatted report of all log entries.
    /// </summary>
    public string GetReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Execution Log Report ===");
        sb.AppendLine($"Total Entries: {_logEntries.Count}");
        sb.AppendLine($"Errors: {_logEntries.Count(e => e.Level == LogLevel.Error)}");
        sb.AppendLine($"Warnings: {_logEntries.Count(e => e.Level == LogLevel.Warning)}");
        sb.AppendLine();

        if (_logEntries.Count > 0)
        {
            sb.AppendLine("Recent Entries:");
            var recentEntries = _logEntries.TakeLast(10);
            foreach (var entry in recentEntries)
            {
                var icon = entry.Level switch
                {
                    LogLevel.Error => "✗",
                    LogLevel.Warning => "⚠",
                    LogLevel.Information => "ℹ",
                    _ => "•"
                };
                sb.AppendLine($"  [{entry.Timestamp:HH:mm:ss}] {icon} {entry.Message}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    public void Clear()
    {
        _logEntries.Clear();
    }

    /// <summary>
    /// Gets summary statistics from log entries.
    /// </summary>
    public LogStatistics GetStatistics()
    {
        var compilationTimes = _logEntries
            .Where(e => e.Metadata.ContainsKey("compilation_time_ms"))
            .Select(e => (long)e.Metadata["compilation_time_ms"]!)
            .ToList();

        var executionTimes = _logEntries
            .Where(e => e.Metadata.ContainsKey("execution_time_ms"))
            .Select(e => (long)e.Metadata["execution_time_ms"]!)
            .ToList();

        return new LogStatistics(
            TotalEntries: _logEntries.Count,
            ErrorCount: _logEntries.Count(e => e.Level == LogLevel.Error),
            WarningCount: _logEntries.Count(e => e.Level == LogLevel.Warning),
            AverageCompilationTimeMs: compilationTimes.Any() ? (long)compilationTimes.Average() : 0,
            MaxCompilationTimeMs: compilationTimes.Any() ? compilationTimes.Max() : 0,
            AverageExecutionTimeMs: executionTimes.Any() ? (long)executionTimes.Average() : 0,
            MaxExecutionTimeMs: executionTimes.Any() ? executionTimes.Max() : 0
        );
    }
}

/// <summary>
/// Single log entry with metadata.
/// </summary>
public record ExecutionLogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    Dictionary<string, object?> Metadata
);

/// <summary>
/// Log severity level.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Aggregated logging statistics.
/// </summary>
public record LogStatistics(
    int TotalEntries,
    int ErrorCount,
    int WarningCount,
    long AverageCompilationTimeMs,
    long MaxCompilationTimeMs,
    long AverageExecutionTimeMs,
    long MaxExecutionTimeMs
)
{
    /// <summary>
    /// Gets human-readable statistics summary.
    /// </summary>
    public string GetSummary()
    {
        return $"Log Statistics: {TotalEntries} entries, {ErrorCount} errors, {WarningCount} warnings. " +
               $"Avg Compilation: {AverageCompilationTimeMs}ms (max: {MaxCompilationTimeMs}ms). " +
               $"Avg Execution: {AverageExecutionTimeMs}ms (max: {MaxExecutionTimeMs}ms).";
    }
}
