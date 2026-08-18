namespace Dialect.Samples.Utilities;

/// <summary>
/// Formats and displays output for examples in console.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Print SQL and parameters for a specific dialect.
    /// </summary>
    public static void PrintDialectResult(
        string dialectName,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        Console.WriteLine($"\n{GetDialectColorPrefix(dialectName)}▸ {dialectName}");
        Console.ResetColor();

        if (sql.StartsWith("ERROR:"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  {sql}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"  SQL:");
            Console.WriteLine($"  {Indent(sql, 4)}");

            if (parameters.Count > 0)
            {
                Console.WriteLine($"\n  Parameters:");
                foreach (var (key, value) in parameters)
                {
                    var displayValue = value == null ? "NULL" : $"'{value}'";
                    Console.WriteLine($"    {key} = {displayValue}");
                }
            }
        }
    }

    /// <summary>
    /// Print a header for a section.
    /// </summary>
    public static void PrintSectionHeader(string title)
    {
        Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
        Console.WriteLine($"  {title}");
        Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
    }

    /// <summary>
    /// Print a subsection header.
    /// </summary>
    public static void PrintSubHeader(string title)
    {
        Console.WriteLine($"\n{title}");
        Console.WriteLine(new string('-', Math.Min(title.Length + 10, 80)));
    }

    /// <summary>
    /// Print colored dialect name prefix.
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
        var indent = new string(' ', spaces);
        return indent + text.Replace("\n", "\n" + indent);
    }

    /// <summary>
    /// Print a success message.
    /// </summary>
    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Print a warning message.
    /// </summary>
    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Print an error message.
    /// </summary>
    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ {message}");
        Console.ResetColor();
    }
}
