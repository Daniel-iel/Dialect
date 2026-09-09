namespace Dialect.Cli.Commands;

using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;
using Dialect.Cli.Security;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// CLI command for translating SQL strings across C# source files.
/// Orchestrates the full workflow: SQL Discovery → Parsing → File Rewriting → Report Generation.
/// 
/// Usage:
///   dialect translate-files --source-dir ./src --output-dir ./reports --from SqlServer --to PostgreSql --patterns "*.cs"
/// 
/// Arguments:
///   --source-dir, -s      Source directory containing C# files to scan (required)
///   --output-dir, -o      Output directory for reports (default: ./reports)
///   --from, -f            Source SQL dialect: SqlServer|PostgreSql|MySql (default: auto-detect)
///   --to, -t              Target SQL dialect: SqlServer|PostgreSql|MySql (required)
///   --patterns, -p        File patterns to match, comma-separated (default: "*.cs")
///   --formats             Report formats: json,md,html (default: all)
///   --no-backup           Skip backup creation (default: creates backups)
///   --dry-run             Preview changes without modifying files (default: false)
///   --verbose, -v         Verbose logging (default: false)
/// </summary>
public sealed class TranslateFilesCommand : ICommand
{
    private readonly BulkFileRewriter _bulkRewriter;
    private readonly ReportingService _reportingService;
    private readonly ISqlTranslator _translator;
    private readonly ILogger<TranslateFilesCommand> _logger;

    public string CommandName => "translate-files";
    public string Description => "Translate SQL strings across C# source files with reporting";

    public TranslateFilesCommand(
        BulkFileRewriter bulkRewriter,
        ReportingService reportingService,
        ISqlTranslator translator,
        ILogger<TranslateFilesCommand> logger)
    {
        _bulkRewriter = bulkRewriter ?? throw new ArgumentNullException(nameof(bulkRewriter));
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the translate-files command.
    /// </summary>
    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            if (string.IsNullOrWhiteSpace(options.SourceDirectory))
            {
                _logger.LogError("--source-dir is required for 'translate-files' command");
                PrintUsage();
                return 1;
            }

            if (!options.TargetDialect.HasValue)
            {
                _logger.LogError("--to (target dialect) is required for 'translate-files' command");
                PrintUsage();
                return 1;
            }

            if (!Directory.Exists(options.SourceDirectory))
            {
                _logger.LogError("Source directory not found: {Directory}", options.SourceDirectory);
                return 1;
            }

            return await ExecuteTranslationAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in translate-files command");
            return 1;
        }
    }

    /// <summary>
    /// Validates security constraints on command options.
    /// Prevents path traversal, validates file accessibility, and sanitizes patterns.
    /// </summary>
    private bool ValidateSecurityOptions(TranslateFilesOptions options)
    {
        var baseDir = Directory.GetCurrentDirectory();

        // Validate source directory
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            _logger.LogError("Source directory is empty");
            return false;
        }

        // If an absolute (rooted) path is provided, allow it if accessible.
        if (Path.IsPathRooted(options.SourceDirectory))
        {
            if (!SecurityValidator.IsDirectoryAccessible(options.SourceDirectory))
            {
                _logger.LogError("Source directory is not accessible or does not exist: {Directory}", options.SourceDirectory);
                return false;
            }
        }
        else
        {
            // For relative paths, ensure they remain within the current working directory
            var resolved = Path.GetFullPath(Path.Combine(baseDir, options.SourceDirectory));
            if (!SecurityValidator.IsPathSafe(resolved, baseDir))
            {
                _logger.LogError("Source directory failed security validation (path traversal or inaccessible): {Directory}", options.SourceDirectory);
                return false;
            }
        }

        // Validate output directory
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            _logger.LogError("Output directory is empty");
            return false;
        }

        // Output directory may be absolute or relative; resolve to full path for checks
        var outputResolved = Path.GetFullPath(Path.Combine(baseDir, options.OutputDirectory));

        if (Path.IsPathRooted(options.OutputDirectory))
        {
            // If the provided output is rooted, ensure it's accessible or creatable
            try
            {
                Directory.CreateDirectory(options.OutputDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create output directory: {Directory}", options.OutputDirectory);
                return false;
            }
        }
        else
        {
            // Ensure relative output directory resolves within the base directory
            if (!SecurityValidator.IsPathSafe(outputResolved, baseDir))
            {
                _logger.LogError("Output directory failed security validation: {Directory}", options.OutputDirectory);
                return false;
            }

            try
            {
                Directory.CreateDirectory(outputResolved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create output directory: {Directory}", outputResolved);
                return false;
            }
        }

        // Validate file patterns
        var safePatterns = SecurityValidator.GetSafeGlobPatterns(string.Join(",", options.FilePatterns));
        if (safePatterns.Count == 0)
        {
            _logger.LogError("No safe file patterns found. All patterns were rejected as unsafe.");
            return false;
        }

        if (safePatterns.Count != options.FilePatterns.Count)
        {
            _logger.LogWarning("Some file patterns were rejected for security reasons. Using: {Patterns}", 
                string.Join(", ", safePatterns));
            options.FilePatterns = safePatterns.ToList();
        }

        return true;
    }

    /// <summary>
    /// Executes the full translation workflow.
    /// </summary>
    private async Task<int> ExecuteTranslationAsync(TranslateFilesOptions options)
    {
        try
        {
            // Security validation before processing
            if (!ValidateSecurityOptions(options))
            {
                _logger.LogError("Security validation failed");
                return 1;
            }

            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Starting SQL file translation workflow");
            _logger.LogInformation("  Source Directory: {Directory}", options.SourceDirectory);
            _logger.LogInformation("  Target Dialect: {Dialect}", options.TargetDialect);
            _logger.LogInformation("  Patterns: {Patterns}", string.Join(", ", options.FilePatterns));
            _logger.LogInformation("  Dry Run: {DryRun}", options.DryRun);

            // Step 1: Discover and rewrite files
            var rewriteOptions = new FileRewriteOptions
            {
                SourceDirectory = options.SourceDirectory,
                FilePatterns = options.FilePatterns.ToList(),
                CreateBackups = options.CreateBackups,
                MaxParallelism = Environment.ProcessorCount
            };

            _logger.LogInformation("Step 1: Discovering and rewriting SQL strings...");
            var rewriteResult = await _bulkRewriter.RewriteFilesAsync(
                rewriteOptions,
                options.SourceDialect,
                options.TargetDialect);

            if (!rewriteResult.Success)
            {
                _logger.LogError("File rewriting failed: {Error}", rewriteResult.Error);
                return 1;
            }

            _logger.LogInformation(
                "Rewrite complete: {ProcessedCount} files processed, {FailedCount} errors",
                rewriteResult.FilesProcessed,
                rewriteResult.Errors.Count);

            // Step 2: Generate reports
            _logger.LogInformation("Step 2: Generating reports...");
            Directory.CreateDirectory(options.OutputDirectory);

            var reportPaths = await _reportingService.GenerateReportsAsync(
                rewriteResult,
                options.OutputDirectory,
                options.ReportFormats.Count > 0 ? options.ReportFormats : null,
                $"SQL Translation Report - {options.SourceDialect} to {options.TargetDialect}");

            _logger.LogInformation("Generated {ReportCount} reports:", reportPaths.Count);
            foreach (var reportPath in reportPaths)
            {
                _logger.LogInformation("  - {Path}", reportPath);
            }

            // Final summary
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Translation workflow complete in {Duration:F2}s", duration.TotalSeconds);

            // Return success if no errors, failure if any errors occurred
            return rewriteResult.Errors.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during translation workflow");
            return 1;
        }
    }

    /// <summary>
    /// Parses command-line arguments into options.
    /// </summary>
    private TranslateFilesOptions ParseArguments(string[] args)
    {
        var options = new TranslateFilesOptions
        {
            SourceDirectory = GetArgumentValue(args, "--source-dir", "-s") ?? "",
            OutputDirectory = GetArgumentValue(args, "--output-dir", "-o") ?? "./reports",
            DryRun = HasFlag(args, "--dry-run"),
            CreateBackups = !HasFlag(args, "--no-backup"),
            Verbose = HasFlag(args, "--verbose", "-v")
        };

        // Parse dialects
        var fromArg = GetArgumentValue(args, "--from", "-f");
        var toArg = GetArgumentValue(args, "--to", "-t");

        if (!string.IsNullOrWhiteSpace(fromArg))
        {
            if (Enum.TryParse<SqlProvider>(fromArg, ignoreCase: true, out var source))
                options.SourceDialect = source;
            else
                _logger.LogWarning("Invalid source dialect: {Dialect}", fromArg);
        }

        if (!string.IsNullOrWhiteSpace(toArg))
        {
            if (Enum.TryParse<SqlProvider>(toArg, ignoreCase: true, out var target))
                options.TargetDialect = target;
            else
                _logger.LogWarning("Invalid target dialect: {Dialect}", toArg);
        }

        // Parse file patterns
        var patternsArg = GetArgumentValue(args, "--patterns", "-p") ?? "*.cs";
        options.FilePatterns = patternsArg
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // Parse report formats
        var formatsArg = GetArgumentValue(args, "--formats");
        if (!string.IsNullOrWhiteSpace(formatsArg))
        {
            options.ReportFormats = formatsArg
                .Split(',')
                .Select(f => f.Trim().ToLowerInvariant())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();
        }

        return options;
    }

    /// <summary>
    /// Gets the value of a named argument.
    /// </summary>
    private static string? GetArgumentValue(string[] args, params string[] names)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (names.Any(n => args[i].Equals(n, StringComparison.OrdinalIgnoreCase)))
                return args[i + 1];
        }
        return null;
    }

    /// <summary>
    /// Checks if a flag is present.
    /// </summary>
    private static bool HasFlag(string[] args, params string[] names)
    {
        return args.Any(arg => names.Any(n => arg.Equals(n, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Prints command usage information.
    /// </summary>
    private void PrintUsage()
    {
        _logger.LogInformation(@"
Usage: dialect translate-files [options]

Options:
  --source-dir, -s <path>       Source directory containing C# files (required)
  --output-dir, -o <path>       Output directory for reports (default: ./reports)
  --from, -f <dialect>          Source dialect: SqlServer|PostgreSql|MySql (default: auto-detect)
  --to, -t <dialect>            Target dialect: SqlServer|PostgreSql|MySql (required)
  --patterns, -p <patterns>     File patterns, comma-separated (default: *.cs)
  --formats <formats>           Report formats: json,md,html (default: all)
  --no-backup                   Skip backup creation
  --dry-run                      Preview changes without modifying files
  --verbose, -v                 Enable verbose logging

Example:
  dialect translate-files -s ./src -o ./reports -f SqlServer -t PostgreSql -v
");
    }
}

/// <summary>
/// Options for file translation command.
/// </summary>
public sealed class TranslateFilesOptions
{
    /// <summary>
    /// Source directory containing C# files to scan.
    /// </summary>
    public string SourceDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Output directory for generated reports.
    /// </summary>
    public string OutputDirectory { get; set; } = "./reports";

    /// <summary>
    /// Source SQL dialect (null = auto-detect).
    /// </summary>
    public SqlProvider? SourceDialect { get; set; }

    /// <summary>
    /// Target SQL dialect.
    /// </summary>
    public SqlProvider? TargetDialect { get; set; }

    /// <summary>
    /// File patterns to match (e.g., "*.cs").
    /// </summary>
    public List<string> FilePatterns { get; set; } = new() { "*.cs" };

    /// <summary>
    /// Report formats to generate (json, md, html).
    /// Empty list = all formats.
    /// </summary>
    public List<string> ReportFormats { get; set; } = new();

    /// <summary>
    /// Whether to create backup files before rewriting.
    /// </summary>
    public bool CreateBackups { get; set; } = true;

    /// <summary>
    /// Whether to preview changes without modifying files.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Whether to enable verbose logging.
    /// </summary>
    public bool Verbose { get; set; }
}
