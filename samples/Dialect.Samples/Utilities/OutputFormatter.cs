namespace Dialect.Samples.Utilities;

/// <summary>
/// Formats and displays output for examples in console.
/// </summary>
public static class OutputFormatter
{
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
    /// Print a success message.
    /// </summary>
    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
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
