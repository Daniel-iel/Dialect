namespace Dialect.Cli.Services;

using Dialect.Cli.CodeGeneration;
using Dialect.Cli.Models;
using Dialect.Cli.SqlDiscovery;
using System.IO;
using System.Linq;

/// <summary>
/// Main orchestrator for C# project SQL-to-FluentBuilder conversion.
/// Coordinates: SQL discovery → code generation → file writing → reporting.
/// </summary>
public sealed class SqlConversionService
{
    private readonly ISqlDiscoveryService _sqlDiscoveryService;
    private readonly IFluentCodeGenerator _codeGenerator;

    public SqlConversionService(
        ISqlDiscoveryService sqlDiscoveryService,
        IFluentCodeGenerator codeGenerator)
    {
        _sqlDiscoveryService = sqlDiscoveryService ?? throw new ArgumentNullException(nameof(sqlDiscoveryService));
        _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
    }

    /// <summary>
    /// Executes conversion for the specified scope (file, directory, project, or solution).
    /// Returns a detailed ConversionReport with results.
    /// </summary>
    public async Task<ConversionReport> ConvertAsync(ConversionOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

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
                    Console.WriteLine($"Processing: {filePath}");

                // Read source file
                var sourceCode = await System.IO.File.ReadAllTextAsync(filePath);

                // Discover SQL strings in the file
                var discoveredSqlStrings = _sqlDiscoveryService.DiscoverSqlStrings(sourceCode, filePath);

                if (discoveredSqlStrings.Count == 0)
                    continue;

                totalSqlFound += discoveredSqlStrings.Count;

                // Convert each SQL string to FluentBuilder code
                var sqlResults = new List<SqlConversionResult>();
                var modifiedSourceCode = sourceCode;
                var fileSuccessful = 0;
                var fileSkipped = 0;
                var fileErrors = 0;

                foreach (var sqlString in discoveredSqlStrings)
                {
                    var convertedCode = _codeGenerator.GenerateFluentCode(sqlString.SqlContent);

                    if (convertedCode is not null)
                    {
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
                        var error = _codeGenerator.GetLastConversionError() ?? "Unknown error";
                        sqlResults.Add(new SqlConversionResult
                        {
                            LineNumber = sqlString.LineNumber,
                            OriginalSql = sqlString.SqlContent,
                            FailureReason = error
                        });

                        if (error.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase))
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
                            Console.WriteLine($"  Backup created: {backupPath}");
                    }

                    // TODO: Apply conversions to source code and write file
                    // For now, we just report what would be done
                    if (options.Verbose)
                        Console.WriteLine($"  Would apply {fileSuccessful} conversions");
                }
            }
            catch (Exception ex)
            {
                if (options.Verbose)
                    Console.Error.WriteLine($"Error processing {filePath}: {ex.Message}");
            }
        }

        return new ConversionReport
        {
            TotalFilesScanned = totalFilesScanned,
            FilesWithSqlFound = results.Count,
            TotalSqlStringsFound = totalSqlFound,
            SuccessfulConversions = successfulConversions,
            SkippedConversions = skippedConversions,
            ConversionErrors = conversionErrors,
            FileResults = results
        };
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
