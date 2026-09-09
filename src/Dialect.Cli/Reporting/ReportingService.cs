using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dialect.Cli.FileRewriting;

namespace Dialect.Cli.Reporting
{
    /// <summary>
    /// Manages multiple report writers and coordinates report generation.
    /// Thread-safe and supports parallel report writing.
    /// </summary>
    public sealed class ReportingService
    {
        private readonly IReadOnlyDictionary<string, IReportWriter> _writers;
        private readonly ILogger<ReportingService> _logger;

        public ReportingService(IReadOnlyDictionary<string, IReportWriter> writers, ILogger<ReportingService> logger)
        {
            _writers = writers ?? throw new ArgumentNullException(nameof(writers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Writes reports in specified formats to the output directory.
        /// </summary>
        /// <param name="result">Bulk file rewrite results.</param>
        /// <param name="outputDirectory">Directory where reports should be written.</param>
        /// <param name="reportFormats">Formats to generate (json, md, html). Null = all formats.</param>
        /// <param name="title">Report title.</param>
        /// <returns>Paths of generated report files.</returns>
        public async Task<IReadOnlyList<string>> GenerateReportsAsync(
            BulkFileRewriteResult result,
            string outputDirectory,
            IReadOnlyList<string>? reportFormats = null,
            string? title = null)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory cannot be empty", nameof(outputDirectory));

            // Create output directory if needed
            Directory.CreateDirectory(outputDirectory);

            // Determine which formats to generate
            var formatsToUse = reportFormats ?? _writers.Keys.ToList();
            
            _logger.LogInformation(
                "Generating reports in formats: {Formats}",
                string.Join(", ", formatsToUse));

            var generatedPaths = new List<string>();
            var tasks = new List<Task>();

            foreach (var format in formatsToUse)
            {
                if (!_writers.TryGetValue(format, out var writer))
                {
                    _logger.LogWarning("Report writer not found for format: {Format}", format);
                    continue;
                }

                var reportTitle = title ?? $"SQL Translation Report - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
                var fileName = $"report.{writer.FileExtension}";
                var outputPath = Path.Combine(outputDirectory, fileName);

                tasks.Add(GenerateReportAsync(writer, result, reportTitle, outputPath, generatedPaths));
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Generated {Count} reports in {Directory}",
                generatedPaths.Count,
                outputDirectory);

            return generatedPaths;
        }

        private async Task GenerateReportAsync(
            IReportWriter writer,
            BulkFileRewriteResult result,
            string title,
            string outputPath,
            List<string> generatedPaths)
        {
            try
            {
                var success = await writer.WriteReportAsync(result, title, outputPath);
                if (success)
                {
                    lock (generatedPaths)
                    {
                        generatedPaths.Add(outputPath);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to write report: {Path}", outputPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report: {Path}", outputPath);
            }
        }

        /// <summary>
        /// Gets available report formats.
        /// </summary>
        public IReadOnlyList<string> AvailableFormats => _writers.Keys.ToList();
    }

    /// <summary>
    /// Factory for creating report writers.
    /// </summary>
    public static class ReportWriterFactory
    {
        /// <summary>
        /// Creates all standard report writers.
        /// </summary>
        public static IReadOnlyDictionary<string, IReportWriter> CreateAll(
            ILogger<JsonReportWriter> jsonLogger,
            ILogger<MarkdownReportWriter> mdLogger,
            ILogger<HtmlDashboardGenerator> htmlLogger)
        {
            return new Dictionary<string, IReportWriter>
            {
                { "json", new JsonReportWriter(jsonLogger) },
                { "md", new MarkdownReportWriter(mdLogger) },
                { "html", new HtmlDashboardGenerator(htmlLogger) }
            };
        }

        /// <summary>
        /// Creates specific report writers.
        /// </summary>
        public static IReportWriter Create(
            string format,
            ILogger<JsonReportWriter> jsonLogger,
            ILogger<MarkdownReportWriter> mdLogger,
            ILogger<HtmlDashboardGenerator> htmlLogger)
        {
            return format.ToLowerInvariant() switch
            {
                "json" => new JsonReportWriter(jsonLogger),
                "md" => new MarkdownReportWriter(mdLogger),
                "html" => new HtmlDashboardGenerator(htmlLogger),
                _ => throw new ArgumentException($"Unknown format: {format}", nameof(format))
            };
        }
    }
}
