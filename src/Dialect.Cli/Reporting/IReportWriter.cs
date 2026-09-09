using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dialect.Cli.FileRewriting;
using Dialect.Core.QueryTranslation;

namespace Dialect.Cli.Reporting
{
    /// <summary>
    /// Interface for generating translation reports in various formats.
    /// Implementations should be thread-safe and stateless.
    /// </summary>
    public interface IReportWriter
    {
        /// <summary>
        /// Gets the file extension for this report format (e.g., "json", "md", "html").
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Generates a report from bulk file rewrite results.
        /// </summary>
        /// <param name="result">Aggregated results from BulkFileRewriter.</param>
        /// <param name="title">Report title/project name.</param>
        /// <param name="outputPath">Path where report should be written.</param>
        /// <returns>True if report generation succeeded.</returns>
        Task<bool> WriteReportAsync(
            BulkFileRewriteResult result,
            string title,
            string outputPath);
    }

    /// <summary>
    /// Report data structure capturing translation statistics and details.
    /// Used across all report formats.
    /// </summary>
    public class TranslationReport
    {
        /// <summary>
        /// Report title (project name, date range, etc.).
        /// </summary>
        public string Title { get; set; } = "SQL Translation Report";

        /// <summary>
        /// Generation timestamp.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Source SQL dialect.
        /// </summary>
        public string SourceDialect { get; set; } = "SQL Server";

        /// <summary>
        /// Target SQL dialect.
        /// </summary>
        public string TargetDialect { get; set; } = "PostgreSQL";

        /// <summary>
        /// Total files processed.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Files with successful rewrites.
        /// </summary>
        public int SuccessfulFiles { get; set; }

        /// <summary>
        /// Files that failed.
        /// </summary>
        public int FailedFiles { get; set; }

        /// <summary>
        /// Total SQL strings translated.
        /// </summary>
        public int TotalTranslations { get; set; }

        /// <summary>
        /// Translation errors encountered.
        /// </summary>
        public int TranslationErrors { get; set; }

        /// <summary>
        /// Per-file translation details.
        /// </summary>
        public IReadOnlyList<FileTranslationDetail> FileDetails { get; set; }
            = Array.Empty<FileTranslationDetail>();

        /// <summary>
        /// Error summaries by type.
        /// </summary>
        public IReadOnlyList<ErrorSummary> ErrorSummaries { get; set; }
            = Array.Empty<ErrorSummary>();

        /// <summary>
        /// Duration of translation process.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Success rate as percentage (0-100).
        /// </summary>
        public double SuccessRate => TotalFiles > 0 ? (SuccessfulFiles * 100.0 / TotalFiles) : 0;

        /// <summary>
        /// Average translations per file.
        /// </summary>
        public double AvgTranslationsPerFile => TotalFiles > 0 ? (TotalTranslations * 1.0 / TotalFiles) : 0;
    }

    /// <summary>
    /// Per-file translation details.
    /// </summary>
    public class FileTranslationDetail
    {
        /// <summary>
        /// File path (relative or absolute).
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// Number of SQL strings translated in this file.
        /// </summary>
        public int TranslationCount { get; set; }

        /// <summary>
        /// Whether the rewrite was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Backup file path if created.
        /// </summary>
        public string? BackupPath { get; set; }

        /// <summary>
        /// Individual translation pairs (before/after).
        /// </summary>
        public IReadOnlyList<(string Before, string After)> TranslationPairs { get; set; }
            = Array.Empty<(string, string)>();
    }

    /// <summary>
    /// Summary of errors by category.
    /// </summary>
    public class ErrorSummary
    {
        /// <summary>
        /// Error category (e.g., "Parse Error", "File Not Found").
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Number of occurrences.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Examples of this error.
        /// </summary>
        public IReadOnlyList<string> Examples { get; set; }
            = Array.Empty<string>();
    }
}
