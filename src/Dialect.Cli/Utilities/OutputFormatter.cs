namespace Dialect.Cli.Utilities;

using Dialect.Cli.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Encapsulates formatted conversion report rendering.
/// Renders box-drawing borders, statistics, and file details via ILogger.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Renders a conversion report with formatted output.
    /// Calls logger.LogInformation for each output line.
    /// </summary>
    public static void RenderReport(ConversionReport report, ILogger logger, bool verbose = false)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        // Header
        logger.LogInformation(@"
╔══════════════════════════════════════════════════════════════╗
║                   CONVERSION REPORT                          ║
╚══════════════════════════════════════════════════════════════╝");

        // Statistics
        logger.LogInformation(@"
Files Scanned:         {FilesScanned}
Files with SQL:        {FilesWithSql}
Total SQL Strings:     {TotalSql}
Successful:            {Successful}
Skipped:               {Skipped}
Errors:                {Errors}
Success Rate:          {SuccessRate:P}",
            report.TotalFilesScanned,
            report.FilesWithSqlFound,
            report.TotalSqlStringsFound,
            report.SuccessfulConversions,
            report.SkippedConversions,
            report.ConversionErrors,
            report.SuccessRate);

        // File details (verbose mode only)
        if (verbose && report.FileResults.Count > 0)
        {
            logger.LogInformation("File Details:");
            foreach (var fileResult in report.FileResults)
            {
                logger.LogInformation("  {FilePath}", fileResult.FilePath);
                foreach (var sqlResult in fileResult.SqlResults)
                {
                    var symbol = sqlResult.IsSuccessful ? "✓" : "✗";
                    logger.LogInformation("    Line {LineNumber}: {Symbol} {Result}",
                        sqlResult.LineNumber, symbol, sqlResult);
                }
            }
        }

        logger.LogInformation("");
    }
}
