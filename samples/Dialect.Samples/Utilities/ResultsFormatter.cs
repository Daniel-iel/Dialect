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
