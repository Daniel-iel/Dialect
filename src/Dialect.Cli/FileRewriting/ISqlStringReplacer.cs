using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;

namespace Dialect.Cli.FileRewriting
{
    /// <summary>
    /// Interface for SQL string replacement in C# source files.
    /// Implementations should be thread-safe and stateless.
    /// </summary>
    public interface ISqlStringReplacer
    {
        /// <summary>
        /// Rewrites a C# source file by translating SQL strings.
        /// Creates a backup before modification if requested.
        /// </summary>
        /// <param name="sourceFilePath">Path to the C# file to rewrite.</param>
        /// <param name="sourceDialect">Detected SQL dialect of the source.</param>
        /// <param name="targetDialect">Target SQL dialect.</param>
        /// <param name="createBackup">Whether to create a .backup file.</param>
        /// <returns>Result with details about replacements or errors.</returns>
        Task<FileRewriteResult> RewriteFileAsync(
            string sourceFilePath,
            SqlProvider sourceDialect,
            SqlProvider targetDialect,
            bool createBackup = true);

        /// <summary>
        /// Restores a file from its backup copy.
        /// </summary>
        /// <param name="backupPath">Path to the .backup file.</param>
        /// <returns>True if restoration succeeded.</returns>
        Task<bool> RestoreFromBackupAsync(string backupPath);

        /// <summary>
        /// Deletes a backup file.
        /// </summary>
        /// <param name="backupPath">Path to the .backup file to delete.</param>
        void DeleteBackup(string backupPath);
    }

    /// <summary>
    /// Result of a file rewrite operation.
    /// Contains details about what was changed, errors, and backup location.
    /// </summary>
    public class FileRewriteResult
    {
        /// <summary>
        /// Whether the file rewrite was successful.
        /// True even if no SQL strings were found.
        /// False if parsing or I/O errors occurred.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Path to the rewritten file.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Number of SQL strings that were translated and replaced.
        /// Zero if no SQL was found or all translations failed.
        /// </summary>
        public int ReplacedCount { get; set; }

        /// <summary>
        /// Path to the backup file (.backup extension).
        /// Null if backup was not created or requested.
        /// </summary>
        public string? BackupPath { get; set; }

        /// <summary>
        /// List of (original, translated) SQL string pairs.
        /// Shows before/after for audit trail.
        /// </summary>
        public IReadOnlyList<(string Original, string Translated)> ReplacedStrings { get; set; } 
            = Array.Empty<(string, string)>();

        /// <summary>
        /// List of translation errors encountered.
        /// File rewrite still succeeds if some strings fail translation.
        /// Only errors from failed translations are included.
        /// </summary>
        public IReadOnlyList<string> Errors { get; set; }
            = Array.Empty<string>();

        /// <summary>
        /// Error message if Success is false.
        /// Examples: file not found, parse error, I/O error.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Options for bulk file rewriting operations.
    /// Controls how files are discovered and processed.
    /// </summary>
    public class FileRewriteOptions
    {
        /// <summary>
        /// Directory to scan for C# files.
        /// </summary>
        public string SourceDirectory { get; set; } = ".";

        /// <summary>
        /// File patterns to include (e.g., "*.cs", "**/*.cs").
        /// Supports glob patterns. Empty means all .cs files.
        /// </summary>
        public IReadOnlyList<string> FilePatterns { get; set; } 
            = new[] { "**/*.cs" };

        /// <summary>
        /// Whether to create .backup files before modifying originals.
        /// </summary>
        public bool CreateBackups { get; set; } = true;

        /// <summary>
        /// Whether to continue processing if a file rewrite fails.
        /// False stops at first error; true collects all errors.
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Maximum number of files to process in parallel.
        /// Default 4 for safe I/O. Increase for faster processing.
        /// </summary>
        public int MaxParallelism { get; set; } = 4;

        /// <summary>
        /// Whether to exclude files in certain directories (node_modules, bin, obj, etc.).
        /// </summary>
        public bool ExcludeCommonBinaryDirectories { get; set; } = true;
    }
}
