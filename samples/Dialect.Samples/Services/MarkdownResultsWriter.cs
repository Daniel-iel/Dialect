using System.Collections;
using System.Text;

namespace Dialect.Samples.Services;

/// <summary>
/// Captures and persists example execution results to a timestamped markdown file.
/// </summary>
public class MarkdownResultsWriter
{
    private readonly List<ExampleResultCapture> _results = new();
    private readonly List<ExampleOutputCapture> _outputs = new();
    private readonly List<ExampleErrorCapture> _errors = new();
    private string? _outputPath;
    private DateTime _executionDateTime;

    /// <summary>
    /// Captured console output from a single example's Run() method.
    /// </summary>
    public record ExampleOutputCapture(
        string ExampleName,
        string Output
    );

    /// <summary>
    /// Result structure for a single example's execution across all dialects.
    /// </summary>
    public record ExampleResultCapture(
        string ExampleName,
        Dictionary<string, DialectResultData> DialectResults
    );

    /// <summary>
    /// Error capture for failed examples.
    /// </summary>
    public record ExampleErrorCapture(
        string ExampleName,
        string ErrorType,
        string ErrorMessage,
        string? StackTrace,
        string? InnerErrorMessage
    );

    /// <summary>
    /// Result data for a specific dialect execution.
    /// </summary>
    public record DialectResultData(
        string Sql,
        Dictionary<string, object?> Parameters,
        List<dynamic> Results,
        long ExecutionTimeMs,
        int RowCount
    );

    /// <summary>
    /// Initialize the writer with a specific timestamp for file naming.
    /// </summary>
    public void Initialize(DateTime? timestamp = null)
    {
        _executionDateTime = timestamp ?? DateTime.Now;
        _results.Clear();
        _outputs.Clear();
        _errors.Clear();
        CreateResultsDirectory();
    }

    /// <summary>
    /// Add the captured console output from an example's Run() method.
    /// </summary>
    public void AddExampleOutput(string exampleName, string output)
    {
        _outputs.Add(new ExampleOutputCapture(exampleName, output));
    }

    /// <summary>
    /// Add an error that occurred during example execution.
    /// </summary>
    public void AddErrorResult(string exampleName, Exception ex)
    {
        var errorCapture = new ExampleErrorCapture(
            exampleName,
            ex.GetType().Name,
            ex.Message,
            ex.StackTrace,
            ex.InnerException?.Message
        );
        _errors.Add(errorCapture);
    }

    /// <summary>
    /// Add execution results for an example.
    /// </summary>
    public void AddExampleResult(
        string exampleName,
        Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters, List<dynamic> Results, long ExecutionTimeMs, int RowCount)> dialectResults)
    {
        var dialectData = new Dictionary<string, DialectResultData>();

        foreach (var (dialectName, (sql, parameters, results, timeMs, rowCount)) in dialectResults)
        {
            dialectData[dialectName] = new DialectResultData(
                sql,
                parameters.ToDictionary(x => x.Key, x => x.Value),
                results,
                timeMs,
                rowCount
            );
        }

        _results.Add(new ExampleResultCapture(exampleName, dialectData));
    }

    /// <summary>
    /// Write all accumulated results to a markdown file and return the file path.
    /// </summary>
    public string SaveToFile()
    {
        var markdown = GenerateMarkdown();
        var fileName = _executionDateTime.ToString("yyyy-MM-dd HH-mm-ss") + ".md";
        _outputPath = Path.Combine(GetResultsDirectory(), fileName);

        File.WriteAllText(_outputPath, markdown, Encoding.UTF8);
        return _outputPath;
    }

    /// <summary>
    /// Generate markdown content from accumulated results.
    /// </summary>
    private string GenerateMarkdown()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Dialect Samples Results");
        sb.AppendLine($"**Execution Date**: {_executionDateTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Examples Executed**: {_results.Count}");
        sb.AppendLine($"**Errors Encountered**: {_errors.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Add error summary if there are any errors
        if (_errors.Count > 0)
        {
            sb.AppendLine("## ⚠️ Errors Encountered");
            sb.AppendLine();
            
            foreach (var error in _errors)
            {
                sb.AppendLine($"### {error.ExampleName}");
                sb.AppendLine();
                sb.AppendLine($"**Error Type**: `{error.ErrorType}`");
                sb.AppendLine();
                sb.AppendLine($"**Message**: {error.ErrorMessage}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(error.InnerErrorMessage))
                {
                    sb.AppendLine($"**Inner Error**: {error.InnerErrorMessage}");
                    sb.AppendLine();
                }
                
                if (!string.IsNullOrEmpty(error.StackTrace))
                {
                    sb.AppendLine("**Stack Trace**:");
                    sb.AppendLine("```");
                    sb.AppendLine(error.StackTrace);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        // Add all example results with structured format
        for (int i = 0; i < _results.Count; i++)
        {
            var result = _results[i];
            sb.AppendLine($"## {i + 1}. {result.ExampleName}");
            sb.AppendLine();
            
            // Compiled SQL section
            sb.AppendLine("### Compiled SQL");
            sb.AppendLine();
            
            foreach (var (dialectName, dialectData) in result.DialectResults)
            {
                sb.AppendLine($"**{dialectName}:**");
                sb.AppendLine();
                sb.AppendLine("```bash");
                sb.AppendLine(dialectData.Sql);
                sb.AppendLine("```");
                sb.AppendLine();
            }
            
            // Parameters section if any exist
            bool hasParameters = result.DialectResults.Values.Any(d => d.Parameters.Count > 0);
            if (hasParameters)
            {
                sb.AppendLine("**Parameters:**");
                sb.AppendLine();
                
                foreach (var (dialectName, dialectData) in result.DialectResults)
                {
                    if (dialectData.Parameters.Count > 0)
                    {
                        sb.AppendLine($"*{dialectName}:*");
                        foreach (var (paramName, paramValue) in dialectData.Parameters)
                        {
                            var displayValue = paramValue == null ? "null" : $"'{paramValue}'";
                            sb.AppendLine($"- `{paramName}` = {displayValue}");
                        }
                    }
                }
                sb.AppendLine();
            }
            
            // Results section
            sb.AppendLine("### Results");
            sb.AppendLine();
            
            // Generate results table based on actual data or fallback to summary
            GenerateResultsTable(sb, result.DialectResults);
            
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extract column names from a dynamic object.
    /// </summary>
    private IEnumerable<string> GetColumnNames(dynamic row)
    {
        try
        {
            if (row is IDictionary<string, object> dict)
            {
                return dict.Keys.ToList();
            }

            // Fallback for other dynamic types
            if (row is IDictionary dict2)
            {
                return dict2.Keys.Cast<string>().ToList();
            }

            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Get a property value from a dynamic object.
    /// </summary>
    private object? GetDynamicProperty(dynamic row, string propertyName)
    {
        try
        {
            if (row is IDictionary<string, object> dict)
            {
                return dict.ContainsKey(propertyName) ? dict[propertyName] : null;
            }

            if (row is IDictionary dict2)
            {
                return dict2[propertyName];
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generate results table from actual execution results.
    /// </summary>
    private void GenerateResultsTable(StringBuilder sb, Dictionary<string, DialectResultData> dialectResults)
    {
        // Check if any dialect has actual results
        bool hasResults = dialectResults.Values.Any(d => d.Results.Count > 0);

        if (!hasResults)
        {
            // No results - use summary format showing "X rows, Yms" per dialect in single row
            sb.AppendLine("| SQL Server | PostgreSQL | MySQL |");
            sb.AppendLine("|-----------|-----------|-----------|");
            
            var rowValues = new List<string>();
            foreach (var (dialectName, dialectData) in dialectResults.OrderBy(x => x.Key))
            {
                rowValues.Add($"{dialectData.RowCount} rows, {dialectData.ExecutionTimeMs}ms");
            }
            sb.AppendLine($"| {string.Join(" | ", rowValues)} |");
            return;
        }

        // We have actual result rows - build dynamic table with columns
        // Find the first non-empty result to get column names
        var firstResultSet = dialectResults.Values.FirstOrDefault(d => d.Results.Count > 0);
        if (firstResultSet?.Results.Count == 0)
        {
            // This shouldn't happen since we checked hasResults above, but handle it gracefully
            sb.AppendLine("| SQL Server | PostgreSQL | MySQL |");
            sb.AppendLine("|-----------|-----------|-----------|");
            var rowValues = new List<string>();
            foreach (var (dialectName, dialectData) in dialectResults.OrderBy(x => x.Key))
            {
                rowValues.Add($"{dialectData.RowCount} rows, {dialectData.ExecutionTimeMs}ms");
            }
            sb.AppendLine($"| {string.Join(" | ", rowValues)} |");
            return;
        }

        // Extract column names from first result row
        if (firstResultSet?.Results == null || firstResultSet.Results.Count == 0)
        {
            // Shouldn't reach here, but handle gracefully
            return;
        }

        var columnNames = GetColumnNames(firstResultSet.Results[0]);
        var columnList = new List<string>(columnNames);

        // Build table header with Database column first, then data columns, then Time and Rows
        var orderedDialects = dialectResults.Keys.OrderBy(x => x).ToList();
        var header = new StringBuilder("| Database |");
        foreach (var column in columnList)
        {
            header.Append($" {column} |");
        }
        header.Append(" Time (ms) | Rows |");
        sb.AppendLine(header.ToString());

        // Build separator
        var separator = new StringBuilder("|----------|");
        foreach (var column in columnList)
        {
            separator.Append("---------|");
        }
        separator.Append("----------|------|");
        sb.AppendLine(separator.ToString());

        // Build data rows - one row per dialect in consistent order
        foreach (var (dialectName, dialectData) in dialectResults.OrderBy(x => x.Key))
        {
            if (dialectData.Results.Count > 0)
            {
                // Show first result row with data
                var row = dialectData.Results[0];
                var rowStr = new StringBuilder($"| {dialectName} |");
                foreach (var column in columnList)
                {
                    var value = GetDynamicProperty(row, column);
                    var displayValue = value == null ? "NULL" : value.ToString() ?? "NULL";
                    rowStr.Append($" {displayValue} |");
                }
                rowStr.Append($" {dialectData.ExecutionTimeMs} | {dialectData.RowCount} |");
                sb.AppendLine(rowStr.ToString());
            }
            else
            {
                // No results for this dialect
                var rowStr = new StringBuilder($"| {dialectName} |");
                foreach (var column in columnList)
                {
                    rowStr.Append(" NULL |");
                }
                rowStr.Append($" {dialectData.ExecutionTimeMs} | {dialectData.RowCount} |");
                sb.AppendLine(rowStr.ToString());
            }
        }
    }

    /// <summary>
    /// Get or create the results directory.
    /// </summary>
    private string GetResultsDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Find the samples root by searching upward for Dialect.Samples.csproj
        var currentDir = new DirectoryInfo(baseDir);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Dialect.Samples.csproj")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            // Fallback to bin output directory
            return Path.Combine(baseDir, "results");
        }

        return Path.Combine(currentDir.FullName, "results");
    }

    /// <summary>
    /// Create the results directory if it doesn't exist.
    /// </summary>
    private void CreateResultsDirectory()
    {
        var directory = GetResultsDirectory();
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
