namespace Dialect.Samples.Utilities;

/// <summary>
/// Formats scenario results for different output contexts (console, markdown, etc).
/// Separates data formatting logic from I/O concerns.
/// </summary>
public static class ResultsFormatter
{
    /// <summary>
    /// Format scenario results for console output.
    /// Shows SQL and parameters for all dialects in a specific format.
    /// </summary>
    public static string FormatForConsole(
        string exampleName,
        Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> results)
    {
        var output = new System.Text.StringBuilder();

        output.AppendLine($"\n{'='.ToString().PadRight(80, '=')}");
        output.AppendLine($"  {exampleName}");
        output.AppendLine($"{'='.ToString().PadRight(80, '=')}");

        foreach (var (dialectName, (sql, parameters)) in results)
        {
            output.AppendLine(FormatDialectResult(dialectName, sql, parameters));
        }

        return output.ToString();
    }

    /// <summary>
    /// Format a single dialect's result (SQL + parameters).
    /// </summary>
    public static string FormatDialectResult(
        string dialectName,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var output = new System.Text.StringBuilder();

        output.AppendLine($"\n{GetDialectColorPrefix(dialectName)}▸ {dialectName}");

        if (sql.StartsWith("ERROR:"))
        {
            output.AppendLine($"  {sql}");
        }
        else
        {
            output.AppendLine($"  SQL:");
            output.AppendLine($"  {Indent(sql, 4)}");

            if (parameters?.Count > 0)
            {
                output.AppendLine($"\n  Parameters:");
                foreach (var (key, value) in parameters)
                {
                    var displayValue = value == null ? "NULL" : $"'{value}'";
                    output.AppendLine($"    {key} = {displayValue}");
                }
            }
        }

        return output.ToString();
    }

    /// <summary>
    /// Format a list of result rows as a Markdown table.
    /// Handles dynamic objects with any property structure.
    /// </summary>
    private static string FormatResultsAsMarkdownTable(List<dynamic> rows)
    {
        if (rows == null || rows.Count == 0)
            return "*No results to display.*";

        var output = new System.Text.StringBuilder();

        // Extract column names from first row
        var firstRow = rows[0] as Dictionary<string, object?>;
        if (firstRow == null)
            return "*Unable to format results.*";

        var columnNames = firstRow.Keys.ToList();

        // Header row
        output.AppendLine("| " + string.Join(" | ", columnNames) + " |");
        output.AppendLine("| " + string.Join(" | ", columnNames.Select(_ => "---")) + " |");

        // Data rows (limit to first 10 to avoid huge tables)
        foreach (var row in rows.Take(10))
        {
            var dict = row as Dictionary<string, object?>;
            if (dict != null)
            {
                var values = columnNames.Select(col =>
                    dict.ContainsKey(col) ? (dict[col]?.ToString() ?? "NULL") : "");
                output.AppendLine("| " + string.Join(" | ", values) + " |");
            }
        }

        if (rows.Count > 10)
        {
            output.AppendLine($"\n*Showing 10 of {rows.Count} rows.*");
        }

        return output.ToString();
    }

    /// <summary>
    /// Format error information.
    /// </summary>
    public static string FormatError(string exampleName, Exception ex)
    {
        var output = new System.Text.StringBuilder();
        output.AppendLine($"\n## ⚠️ Error in {exampleName}");
        output.AppendLine($"\n**Error Type:** `{ex.GetType().Name}`");
        output.AppendLine($"\n**Message:** {ex.Message}");
        if (ex.InnerException != null)
        {
            output.AppendLine($"\n**Inner Error:** {ex.InnerException.Message}");
        }
        output.AppendLine($"\n**Stack Trace:**");
        output.AppendLine($"```");
        output.AppendLine(ex.StackTrace);
        output.AppendLine($"```");

        return output.ToString();
    }

    /// <summary>
    /// Get colored ANSI prefix for dialect name (for console output).
    /// </summary>
    private static string GetDialectColorPrefix(string dialectName)
    {
        return dialectName switch
        {
            "SQL Server" => "\u001b[38;5;33m",  // Blue
            "PostgreSQL" => "\u001b[38;5;154m", // Green
            "MySQL" => "\u001b[38;5;208m",      // Orange
            _ => ""
        };
    }

    /// <summary>
    /// Indent text by a specific number of spaces.
    /// </summary>
    private static string Indent(string text, int spaces)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var indent = new string(' ', spaces);
        return indent + text.Replace("\n", "\n" + indent);
    }

    /// <summary>
    /// Create a separator line.
    /// </summary>
    public static string CreateSeparator(int width = 80, char character = '=')
    {
        return new string(character, width);
    }

    /// <summary>
    /// Format success message.
    /// </summary>
    public static string FormatSuccess(string message)
    {
        return $"✓ {message}";
    }

    /// <summary>
    /// Format warning message.
    /// </summary>
    public static string FormatWarning(string message)
    {
        return $"⚠ {message}";
    }
}
