namespace Dialect.Cli.Services;

using Dialect.Cli.Models;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.QueryTranslation;
using ErrorOr;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

/// <summary>
/// Main orchestrator for C# project SQL-to-FluentBuilder conversion.
/// Coordinates: SQL discovery → code generation → file writing → reporting.
/// </summary>
public sealed class SqlConversionService
{
    private readonly ISqlDiscoveryService _sqlDiscoveryService;
    private readonly ISqlTranslator _sqlTranslator;
    private readonly ILogger<SqlConversionService> _logger;

    public SqlConversionService(
        ISqlDiscoveryService sqlDiscoveryService,
        ISqlTranslator sqlTranslator,
        ILogger<SqlConversionService> logger)
    {
        _sqlDiscoveryService = sqlDiscoveryService ?? throw new ArgumentNullException(nameof(sqlDiscoveryService));
        _sqlTranslator = sqlTranslator ?? throw new ArgumentNullException(nameof(sqlTranslator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes conversion for the specified scope (file, directory, project, or solution).
    /// Returns a Result containing a detailed ConversionReport.
    /// </summary>
    public async Task<ErrorOr<ConversionReport>> ConvertAsync(ConversionOptions options)
    {
        if (options == null)
            return Error.Validation("options", "Conversion options cannot be null");

        try
        {
            var results = new List<FileConversionResult>();
            var totalFilesScanned = 0;
            var totalSqlFound = 0;
            var successfulConversions = 0;
            var skippedConversions = 0;
            var conversionErrors = 0;

            // Get list of C# files to process based on scope
            var filesToProcess = GetFilesToProcess(options.Scope);

            foreach (var filePath in filesToProcess)
            {
                try
                {
                    totalFilesScanned++;

                    if (options.Verbose)
                        _logger.LogInformation("Processing: {FilePath}", filePath);

                    // Read source file
                    var sourceCode = await System.IO.File.ReadAllTextAsync(filePath);

                    // Discover SQL strings in the file
                    var discoveredSqlStrings = _sqlDiscoveryService.DiscoverSqlStrings(sourceCode, filePath);

                    if (discoveredSqlStrings.Count == 0)
                        continue;

                    totalSqlFound += discoveredSqlStrings.Count;

                    // Convert each SQL string to target dialect SQL text
                    var sqlResults = new List<SqlConversionResult>();
                    var modifiedSourceCode = sourceCode;
                    var fileSuccessful = 0;
                    var fileSkipped = 0;
                    var fileErrors = 0;

                    foreach (var sqlString in discoveredSqlStrings)
                    {
                        var translation = TranslateSql(sqlString.SqlContent, options);
                        if (translation.HasCompiledResult)
                        {
                            var convertedCode = ToCSharpStringLiteral(translation.Compiled!.Sql);
                            sqlResults.Add(new SqlConversionResult
                            {
                                LineNumber = sqlString.LineNumber,
                                OriginalSql = sqlString.SqlContent,
                                ConvertedCode = convertedCode
                            });
                            fileSuccessful++;
                            successfulConversions++;
                        }
                        else
                        {
                            var failureReason = translation.ErrorMessage;
                            if (string.IsNullOrWhiteSpace(failureReason) && translation.UntranslatableConstructs.Count > 0)
                            {
                                failureReason = string.Join("; ", translation.UntranslatableConstructs);
                            }
                            failureReason ??= "Unknown translation error";

                            sqlResults.Add(new SqlConversionResult
                            {
                                LineNumber = sqlString.LineNumber,
                                OriginalSql = sqlString.SqlContent,
                                FailureReason = failureReason
                            });

                            if (failureReason.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase))
                            {
                                fileSkipped++;
                                skippedConversions++;
                            }
                            else
                            {
                                fileErrors++;
                                conversionErrors++;
                            }
                        }
                    }

                    // Create file result
                    results.Add(new FileConversionResult
                    {
                        FilePath = filePath,
                        SqlStringsFound = discoveredSqlStrings.Count,
                        SuccessfulConversions = fileSuccessful,
                        SkippedConversions = fileSkipped,
                        SqlResults = sqlResults
                    });

                    // Write modified file (if not dry-run)
                    if (!options.DryRun && fileSuccessful > 0)
                    {
                        if (options.CreateBackups)
                        {
                            var backupPath = filePath + ".bak";
                            System.IO.File.Copy(filePath, backupPath, overwrite: true);
                            if (options.Verbose)
                                _logger.LogInformation("Backup created: {BackupPath}", backupPath);
                        }

                        // Apply conversions to source code and write file
                        // We replace each discovered literal with the generated code snippet.
                        try
                        {
                            // Work on normalized line endings to compute positions reliably
                            var normalized = modifiedSourceCode.Replace("\r\n", "\n");
                            var lines = normalized.Split('\n').ToList();

                            // Process discovered strings in reverse order to avoid shifting indices
                            var ordered = discoveredSqlStrings
                                .OrderByDescending(d => d.LineNumber)
                                .ThenByDescending(d => d.ColumnNumber)
                                .ToList();

                            var applied = 0;
                            foreach (var ds in ordered)
                            {
                                var originalLiteral = ds.OriginalLiteral ?? ds.SqlContent;
                                var lineIdx = Math.Max(0, ds.LineNumber - 1);
                                if (lineIdx >= lines.Count) continue;

                                var line = lines[lineIdx];
                                var startPos = Math.Max(0, ds.ColumnNumber - 1);
                                var idx = line.IndexOf(originalLiteral, startPos, StringComparison.Ordinal);
                                if (idx < 0)
                                {
                                    // fallback: search anywhere on the line
                                    idx = line.IndexOf(originalLiteral, StringComparison.Ordinal);
                                }

                                if (idx < 0)
                                    continue;

                                var resultMatch = sqlResults.FirstOrDefault(r => r.OriginalSql == ds.SqlContent && !string.IsNullOrEmpty(r.ConvertedCode));
                                if (resultMatch == null)
                                    continue;

                                var converted = resultMatch.ConvertedCode!;

                                // Preserve indentation of the original literal's column
                                var indent = line.Take(idx).Count(c => c == ' ' || c == '\t');
                                var indentStr = new string(' ', indent);
                                var convertedIndented = string.Join("\n", converted.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                                    .Select((ln, i) => i == 0 ? ln : indentStr + ln));

                                // Replace the literal with the converted snippet
                                lines[lineIdx] = line.Substring(0, idx) + convertedIndented + line.Substring(idx + originalLiteral.Length);
                                applied++;
                            }

                            if (applied > 0)
                            {
                                // Restore original line endings style (use Environment.NewLine)
                                var newContent = string.Join(Environment.NewLine, lines);
                                System.IO.File.WriteAllText(filePath, newContent);
                                if (options.Verbose)
                                    _logger.LogInformation("Applied {Applied} conversions to {FilePath}", applied, filePath);
                            }
                            else
                            {
                                if (options.Verbose)
                                    _logger.LogInformation("No applicable conversions found to apply for {FilePath}", filePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to apply conversions to {FilePath}", filePath);
                            conversionErrors++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing {FilePath}", filePath);
                    conversionErrors++;
                }
            }

            var report = new ConversionReport
            {
                TotalFilesScanned = totalFilesScanned,
                FilesWithSqlFound = results.Count,
                TotalSqlStringsFound = totalSqlFound,
                SuccessfulConversions = successfulConversions,
                SkippedConversions = skippedConversions,
                ConversionErrors = conversionErrors,
                FileResults = results
            };

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during conversion");
            return Error.Failure("conversion.fatal", $"Fatal error: {ex.Message}");
        }
    }

    private TranslationResult TranslateSql(string sqlContent, ConversionOptions options)
    {
        if (options.SourceProvider.HasValue)
        {
            return _sqlTranslator.Translate(sqlContent, options.SourceProvider.Value, options.TargetDialect);
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return _sqlTranslator.Translate(sqlContent, options.ConnectionString, options.TargetDialect);
        }

        return _sqlTranslator.Translate(
            sqlContent,
            sourceProvider: null,
            targetProvider: ResolveTargetProvider(options.TargetDialect));
    }

    private static SqlProvider ResolveTargetProvider(ISqlDialect targetDialect)
    {
        if (targetDialect.ParameterPrefix == ":" || targetDialect.IdentifierQuote == '"')
            return SqlProvider.PostgreSql;

        if (targetDialect.ParameterPrefix == "?" || targetDialect.IdentifierQuote == '`')
            return SqlProvider.MySql;

        return SqlProvider.SqlServer;
    }

    private static string ToCSharpStringLiteral(string sql)
    {
        var escaped = sql
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Gets list of C# files to process based on conversion scope.
    /// </summary>
    private static IReadOnlyList<string> GetFilesToProcess(ConversionScope scope)
    {
        return scope switch
        {
            ConversionScope.SingleFile singleFile =>
                System.IO.File.Exists(singleFile.FilePath)
                    ? [singleFile.FilePath]
                    : throw new FileNotFoundException($"File not found: {singleFile.FilePath}"),

            ConversionScope.Directory directory =>
                Directory.GetFiles(
                    directory.DirectoryPath,
                    directory.SearchPattern,
                    SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToList(),

            ConversionScope.Project project =>
                GetFilesFromProject(project.ProjectFilePath),

            ConversionScope.Solution solution =>
                GetFilesFromSolution(solution.SolutionFilePath),

            _ => throw new ArgumentException($"Unknown conversion scope: {scope.GetType().Name}")
        };
    }

    /// <summary>
    /// Parses a .csproj file and extracts all C# source files.
    /// (Placeholder: currently returns empty list)
    /// </summary>
    private static IReadOnlyList<string> GetFilesFromProject(string projectFilePath)
    {
        if (!System.IO.File.Exists(projectFilePath))
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");

        // TODO: Parse .csproj XML and extract Compile items
        // For now, return all .cs files in project directory
        var projectDir = System.IO.Path.GetDirectoryName(projectFilePath)
            ?? throw new InvalidOperationException("Cannot determine project directory");

        return Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories).ToList();
    }

    /// <summary>
    /// Parses a .sln file and gets all projects, then all their source files.
    /// (Placeholder: currently returns empty list)
    /// </summary>
    private static IReadOnlyList<string> GetFilesFromSolution(string solutionFilePath)
    {
        if (!System.IO.File.Exists(solutionFilePath))
            throw new FileNotFoundException($"Solution file not found: {solutionFilePath}");

        // TODO: Parse .sln file and extract project references
        // For now, return all .cs files in solution directory
        var solutionDir = System.IO.Path.GetDirectoryName(solutionFilePath)
            ?? throw new InvalidOperationException("Cannot determine solution directory");

        return Directory.GetFiles(solutionDir, "*.cs", SearchOption.AllDirectories).ToList();
    }
}
