using Dialect.Samples.Utilities;

namespace Dialect.Samples.Services;

/// <summary>
/// Reports execution progress to the console without printing results.
/// Shows only which examples are running and their status.
/// </summary>
public class ExecutionProgressReporter
{
    private int _totalExamples;
    private int _completedExamples;

    /// <summary>
    /// Initialize the reporter with the total number of examples to run.
    /// </summary>
    public void Initialize(int totalExamples)
    {
        _totalExamples = totalExamples;
        _completedExamples = 0;

        OutputFormatter.PrintSectionHeader("EXECUTING ALL EXAMPLES (RESULTS TO MARKDOWN)");
        Console.WriteLine($"\nRunning {totalExamples} examples...\n");
    }

    /// <summary>
    /// Report that an example is starting execution.
    /// </summary>
    public void ReportStarting(string exampleName)
    {
        Console.Write($"Running {exampleName}...");
    }

    /// <summary>
    /// Report that an example completed successfully.
    /// </summary>
    public void ReportCompleted(string exampleName)
    {
        _completedExamples++;
        Console.WriteLine($"");
        var progressPercent = (_completedExamples * 100) / _totalExamples;
        Console.WriteLine($"   Progress: {_completedExamples}/{_totalExamples} ({progressPercent}%)");
    }

    /// <summary>
    /// Report that an example failed with an error.
    /// </summary>
    public void ReportError(string exampleName, Exception? ex = null)
    {
        _completedExamples++;
        Console.WriteLine($" ✗");
        if (ex != null)
        {
            // Log detailed error information to console
            OutputFormatter.PrintError($"   Error: {ex.GetType().Name}");
            OutputFormatter.PrintError($"   Message: {ex.Message}");

            // Show inner exception if exists
            if (ex.InnerException != null)
            {
                OutputFormatter.PrintError($"   Inner Exception: {ex.InnerException.GetType().Name}");
                OutputFormatter.PrintError($"   Inner Message: {ex.InnerException.Message}");

                // Show second level inner exception if exists
                if (ex.InnerException.InnerException != null)
                {
                    OutputFormatter.PrintError($"   Root Cause: {ex.InnerException.InnerException.GetType().Name}");
                    OutputFormatter.PrintError($"   Root Message: {ex.InnerException.InnerException.Message}");
                }
            }

            // Log stack trace if in debug or verbose mode
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                OutputFormatter.PrintError($"   Stack Trace:\n{ex.StackTrace}");
            }
        }
        var progressPercent = (_completedExamples * 100) / _totalExamples;
        Console.WriteLine($"   Progress: {_completedExamples}/{_totalExamples} ({progressPercent}%)");
    }

    /// <summary>
    /// Report the final output file path.
    /// </summary>
    public void ReportFinalPath(string filePath)
    {
        Console.WriteLine();
        OutputFormatter.PrintSectionHeader("EXECUTION COMPLETE");
        OutputFormatter.PrintSuccess($"\nResults saved to:\n   {filePath}\n");
    }

    /// <summary>
    /// Report a warning during query execution (e.g., failed database connection).
    /// </summary>
    public void ReportExecutionWarning(string message)
    {
        // Silently log warnings - don't interrupt progress display
        // Errors will be visible in the final markdown report
    }
}
