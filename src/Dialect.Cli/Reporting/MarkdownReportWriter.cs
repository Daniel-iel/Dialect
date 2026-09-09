using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dialect.Cli.FileRewriting;

namespace Dialect.Cli.Reporting
{
    /// <summary>
    /// Markdown report writer for human-readable documentation.
    /// Generates formatted reports suitable for version control and communication.
    /// </summary>
    public sealed class MarkdownReportWriter : IReportWriter
    {
        private readonly ILogger<MarkdownReportWriter> _logger;

        public string FileExtension => "md";

        public MarkdownReportWriter(ILogger<MarkdownReportWriter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> WriteReportAsync(
            BulkFileRewriteResult result,
            string title,
            string outputPath)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

            try
            {
                var sb = new StringBuilder();

                // Build report structure
                var report = new TranslationReport
                {
                    Title = title,
                    GeneratedAt = DateTime.UtcNow,
                    TotalFiles = result.TotalFilesFound,
                    SuccessfulFiles = result.FilesProcessed,
                    FailedFiles = result.TotalFilesFound - result.FilesProcessed,
                    TotalTranslations = result.TotalReplacements,
                    TranslationErrors = result.Errors.Count,
                    FileDetails = BuildFileDetails(result),
                    ErrorSummaries = BuildErrorSummaries(result)
                };

                // Render Markdown
                RenderMarkdown(sb, report);

                // Write to file
                await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
                _logger.LogInformation("Markdown report written to: {OutputPath}", outputPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing Markdown report to {OutputPath}: {Message}", outputPath, ex.Message);
                return false;
            }
        }

        private void RenderMarkdown(StringBuilder sb, TranslationReport report)
        {
            // Header
            sb.AppendLine($"# {report.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            // Summary
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Total Files | {report.TotalFiles} |");
            sb.AppendLine($"| Successful | {report.SuccessfulFiles} ✅ |");
            sb.AppendLine($"| Failed | {report.FailedFiles} ❌ |");
            sb.AppendLine($"| Success Rate | {report.SuccessRate:F1}% |");
            sb.AppendLine($"| Total Translations | {report.TotalTranslations} |");
            sb.AppendLine($"| Avg per File | {report.AvgTranslationsPerFile:F2} |");
            sb.AppendLine($"| Translation Errors | {report.TranslationErrors} |");
            sb.AppendLine();

            // Dialects
            sb.AppendLine("## Translation Configuration");
            sb.AppendLine();
            sb.AppendLine($"- **Source:** {report.SourceDialect}");
            sb.AppendLine($"- **Target:** {report.TargetDialect}");
            sb.AppendLine();

            // File details
            if (report.FileDetails.Any())
            {
                sb.AppendLine("## File Details");
                sb.AppendLine();
                sb.AppendLine("| File | Status | Translations | Error |");
                sb.AppendLine("|------|--------|--------------|-------|");

                foreach (var file in report.FileDetails)
                {
                    var status = file.Success ? "✅" : "❌";
                    var error = file.Error?.Substring(0, Math.Min(40, file.Error.Length)) ?? "-";
                    var safePath = file.FilePath.Replace("|", "\\|");
                    sb.AppendLine($"| {safePath} | {status} | {file.TranslationCount} | {error} |");
                }
                sb.AppendLine();
            }

            // Error summary
            if (report.ErrorSummaries.Any())
            {
                sb.AppendLine("## Error Summary");
                sb.AppendLine();
                
                foreach (var errorSummary in report.ErrorSummaries)
                {
                    sb.AppendLine($"### {errorSummary.Category}");
                    sb.AppendLine();
                    sb.AppendLine($"**Count:** {errorSummary.Count}");
                    sb.AppendLine();
                    sb.AppendLine("**Examples:**");
                    sb.AppendLine();
                    foreach (var example in errorSummary.Examples)
                    {
                        sb.AppendLine($"- {example}");
                    }
                    sb.AppendLine();
                }
            }

            // Footer
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("*Report generated by Dialect SQL Translation Tool*");
        }

        private List<FileTranslationDetail> BuildFileDetails(BulkFileRewriteResult result)
        {
            return result.FileResults
                .Select(fr => new FileTranslationDetail
                {
                    FilePath = fr.FilePath ?? "unknown",
                    TranslationCount = fr.ReplacedCount,
                    Success = fr.Success,
                    Error = fr.Error,
                    BackupPath = fr.BackupPath
                })
                .ToList();
        }

        private List<ErrorSummary> BuildErrorSummaries(BulkFileRewriteResult result)
        {
            if (!result.Errors.Any())
                return new List<ErrorSummary>();

            var errorGroups = new Dictionary<string, List<string>>();

            foreach (var error in result.Errors)
            {
                var category = ExtractErrorCategory(error);
                if (!errorGroups.ContainsKey(category))
                    errorGroups[category] = new List<string>();

                errorGroups[category].Add(error);
            }

            return errorGroups
                .Select(kvp => new ErrorSummary
                {
                    Category = kvp.Key,
                    Count = kvp.Value.Count,
                    Examples = kvp.Value.Take(3).ToList()
                })
                .ToList();
        }

        private string ExtractErrorCategory(string error)
        {
            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return "File Not Found";
            if (error.Contains("parse", StringComparison.OrdinalIgnoreCase))
                return "Parse Error";
            if (error.Contains("translation", StringComparison.OrdinalIgnoreCase))
                return "Translation Error";
            if (error.Contains("backup", StringComparison.OrdinalIgnoreCase))
                return "Backup Error";
            if (error.Contains("permission", StringComparison.OrdinalIgnoreCase))
                return "Permission Error";

            return "Other Error";
        }
    }
}
