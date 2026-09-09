using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;

namespace Dialect.Cli.FileRewriting
{
    /// <summary>
    /// Bulk file processor for translating SQL strings across multiple C# files.
    /// Supports parallel processing with configurable options.
    /// Thread-safe: can be used concurrently.
    /// </summary>
    public sealed class BulkFileRewriter
    {
        private readonly ISqlStringReplacer _rewriter;
        private readonly ILogger<BulkFileRewriter> _logger;

        public BulkFileRewriter(
            ISqlStringReplacer rewriter,
            ILogger<BulkFileRewriter> logger)
        {
            _rewriter = rewriter ?? throw new ArgumentNullException(nameof(rewriter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes all C# files in a directory, translating SQL strings.
        /// Returns aggregated results across all files.
        /// </summary>
        public async Task<BulkFileRewriteResult> RewriteFilesAsync(
            FileRewriteOptions options,
            SqlProvider? sourceDialect = null,
            SqlProvider? targetDialect = null)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!Directory.Exists(options.SourceDirectory))
                return new BulkFileRewriteResult
                {
                    Success = false,
                    Error = $"Directory not found: {options.SourceDirectory}"
                };

            if (!targetDialect.HasValue)
                targetDialect = SqlProvider.PostgreSql; // Default target

            _logger.LogInformation(
                "Starting bulk file rewrite in: {Directory}",
                options.SourceDirectory);

            // Discovery: Find all .cs files matching patterns
            var filesToProcess = DiscoverFiles(options).ToList();
            _logger.LogInformation("Discovered {FileCount} C# files to process", filesToProcess.Count);

            if (filesToProcess.Count == 0)
                return new BulkFileRewriteResult
                {
                    Success = true,
                    Error = "No files found matching patterns"
                };

            // Process files in parallel
            var results = new List<FileRewriteResult>();
            var processedFiles = 0;
            var errors = new List<string>();

            using (var semaphore = new System.Threading.SemaphoreSlim(options.MaxParallelism))
            {
                var tasks = filesToProcess.Select(async filePath =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        // Detect source dialect if not specified
                        SqlProvider dialect = sourceDialect ?? SqlProvider.SqlServer;

                        // Rewrite file
                        var result = await _rewriter.RewriteFileAsync(
                            filePath,
                            dialect,
                            targetDialect.Value,
                            options.CreateBackups);

                        lock (results)
                        {
                            results.Add(result);
                            if (result.Success)
                            {
                                processedFiles++;
                                _logger.LogInformation(
                                    "✓ {File}: {Count} strings replaced",
                                    Path.GetFileName(filePath),
                                    result.ReplacedCount);
                            }
                            else
                            {
                                errors.Add($"{filePath}: {result.Error}");
                                if (!options.ContinueOnError)
                                {
                                    _logger.LogError("✗ {File}: {Error}", filePath, result.Error);
                                }
                            }
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception processing {File}: {Message}", filePath, ex.Message);
                        if (!options.ContinueOnError)
                            throw;
                        errors.Add($"{filePath}: {ex.Message}");
                        return null;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            // Aggregate results
            var totalReplacements = results.Where(r => r.Success).Sum(r => r.ReplacedCount);
            var successCount = results.Count(r => r.Success);

            var aggregatedResult = new BulkFileRewriteResult
            {
                Success = errors.Count == 0,
                FilesProcessed = successCount,
                TotalFilesFound = filesToProcess.Count,
                TotalReplacements = totalReplacements,
                FileResults = results,
                Errors = errors
            };

            _logger.LogInformation(
                "Bulk rewrite complete: {Successful}/{Total} files processed, {Replacements} total replacements",
                successCount,
                filesToProcess.Count,
                totalReplacements);

            return aggregatedResult;
        }

        /// <summary>
        /// Discovers C# files matching the specified patterns and options.
        /// </summary>
        private IEnumerable<string> DiscoverFiles(FileRewriteOptions options)
        {
            var baseDir = new DirectoryInfo(options.SourceDirectory);
            var patterns = options.FilePatterns.Any() ? options.FilePatterns : new[] { "**/*.cs" };

            var discoveredFiles = new List<string>();

            foreach (var pattern in patterns)
            {
                var normalizedPattern = pattern.Replace("\\", "/");
                if (normalizedPattern.StartsWith("**/"))
                {
                    // Recursive pattern
                    discoveredFiles.AddRange(
                        Directory.EnumerateFiles(
                            baseDir.FullName,
                            "*.cs",
                            SearchOption.AllDirectories));
                }
                else
                {
                    // Single-level pattern
                    discoveredFiles.AddRange(
                        Directory.EnumerateFiles(
                            baseDir.FullName,
                            pattern,
                            SearchOption.TopDirectoryOnly));
                }
            }

            // Filter by exclusion rules
            if (options.ExcludeCommonBinaryDirectories)
            {
                var excludedDirs = new[] { "bin", "obj", "node_modules", ".git", "packages", ".nuget" };
                discoveredFiles = discoveredFiles
                    .Where(f => !excludedDirs.Any(d => f.Contains($"/{d}/") || f.Contains($"\\{d}\\")))
                    .ToList();
            }

            return discoveredFiles.Distinct().OrderBy(f => f);
        }
    }

    /// <summary>
    /// Result of a bulk file rewrite operation.
    /// Aggregates results across all processed files.
    /// </summary>
    public class BulkFileRewriteResult
    {
        /// <summary>
        /// Whether the bulk operation succeeded (all files processed without critical errors).
        /// True if all files were processed; false if operation was aborted.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of files that were successfully processed.
        /// </summary>
        public int FilesProcessed { get; set; }

        /// <summary>
        /// Total number of files discovered matching patterns.
        /// </summary>
        public int TotalFilesFound { get; set; }

        /// <summary>
        /// Total number of SQL strings replaced across all files.
        /// </summary>
        public int TotalReplacements { get; set; }

        /// <summary>
        /// Individual result for each file processed.
        /// Used for detailed reporting and audit trails.
        /// </summary>
        public IReadOnlyList<FileRewriteResult> FileResults { get; set; }
            = Array.Empty<FileRewriteResult>();

        /// <summary>
        /// List of errors encountered.
        /// May contain file-level or operation-level errors.
        /// </summary>
        public IReadOnlyList<string> Errors { get; set; }
            = Array.Empty<string>();

        /// <summary>
        /// Summary error message if Success is false.
        /// </summary>
        public string? Error { get; set; }
    }
}
