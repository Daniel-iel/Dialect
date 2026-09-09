using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dialect.Cli.FileRewriting;

namespace Dialect.Cli.Reporting
{
    /// <summary>
    /// JSON report writer for structured machine-readable output.
    /// Generates reports compatible with CI/CD pipelines and integrations.
    /// </summary>
    public sealed class JsonReportWriter : IReportWriter
    {
        private readonly ILogger<JsonReportWriter> _logger;

        public string FileExtension => "json";

        public JsonReportWriter(ILogger<JsonReportWriter> logger)
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

                // Serialize to JSON with formatting
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };

                var json = JsonSerializer.Serialize(report, options);

                // Write to file
                await File.WriteAllTextAsync(outputPath, json);
                _logger.LogInformation("JSON report written to: {OutputPath}", outputPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing JSON report to {OutputPath}: {Message}", outputPath, ex.Message);
                return false;
            }
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
                    BackupPath = fr.BackupPath,
                    TranslationPairs = fr.ReplacedStrings
                        .Select(p => (
                            Before: Dialect.Cli.Security.SecurityValidator.SanitizeSqlForLogging(p.Original),
                            After: Dialect.Cli.Security.SecurityValidator.SanitizeSqlForLogging(p.Translated)
                        ))
                        .ToList()
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
