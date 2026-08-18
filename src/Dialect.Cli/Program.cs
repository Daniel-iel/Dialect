using Dialect.Cli.CodeGeneration;
using Dialect.Cli.Models;
using Dialect.Cli.Services;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Dialect.Cli;

/// <summary>
/// SQL-to-FluentBuilder C# converter CLI.
/// Discovers SQL strings in C# source and converts them to FluentBuilder API calls.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Parse basic CLI arguments (simplified for now without System.CommandLine 2.0.0)
            string? path = null;
            string? sourceProvider = null;
            string? connectionString = null;
            string targetProvider = "SqlServer";
            bool dryRun = true;
            bool apply = false;
            bool verbose = false;
            bool createBackups = true;

            // Simple argument parsing
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--source-provider":
                    case "-sp":
                        sourceProvider = i + 1 < args.Length ? args[++i] : null;
                        break;
                    case "--connection-string":
                    case "-cs":
                        connectionString = i + 1 < args.Length ? args[++i] : null;
                        break;
                    case "--target-provider":
                    case "-tp":
                        targetProvider = i + 1 < args.Length ? args[++i] : "SqlServer";
                        break;
                    case "--dry-run":
                    case "-d":
                        dryRun = true;
                        break;
                    case "--apply":
                    case "-a":
                        apply = true;
                        dryRun = false;
                        break;
                    case "--verbose":
                    case "-v":
                        verbose = true;
                        break;
                    case "--no-backup":
                        createBackups = false;
                        break;
                    default:
                        if (!args[i].StartsWith("-"))
                        {
                            path = args[i];
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Usage: dialect-cli <path> [options]");
                Console.WriteLine("Options:");
                Console.WriteLine("  --source-provider, -sp <provider>   Source SQL provider (SqlServer, PostgreSql, MySql)");
                Console.WriteLine("  --connection-string, -cs <string>   Connection string for provider auto-detection");
                Console.WriteLine("  --target-provider, -tp <provider>   Target SQL dialect (default: SqlServer)");
                Console.WriteLine("  --dry-run, -d                        Perform dry-run without modifying files");
                Console.WriteLine("  --apply, -a                          Apply conversions and modify files");
                Console.WriteLine("  --verbose, -v                        Print detailed diagnostic information");
                Console.WriteLine("  --no-backup                          Skip backup file creation");
                return 1;
            }

            // Execute conversion
            await HandleConversionAsync(path, sourceProvider, connectionString, targetProvider, dryRun, verbose, createBackups);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Main conversion handler called from CLI.
    /// </summary>
    static async Task HandleConversionAsync(
        string path,
        string? sourceProvider,
        string? connectionString,
        string targetProvider,
        bool dryRun,
        bool verbose,
        bool createBackups)
    {
        try
        {
            // Setup dependency injection
            var services = new ServiceCollection();

            // Register SQL translation services
            services.AddSqlTranslation();

            // Register CLI services
            services.AddSingleton<ISqlDiscoveryService, RoslynSqlDiscoveryService>();
            services.AddSingleton<IFluentCodeGenerator, DefaultFluentCodeGenerator>();
            services.AddSingleton<SqlConversionService>();

            var serviceProvider = services.BuildServiceProvider();

            // Parse arguments
            var scope = ParseConversionScope(path);
            var targetDialect = ParseTargetDialect(targetProvider);

            // Build conversion options
            var options = new ConversionOptions
            {
                Scope = scope,
                SourceProvider = sourceProvider is not null ? Enum.Parse<Dialect.Core.AST.SqlProvider>(sourceProvider) : null,
                ConnectionString = connectionString,
                TargetDialect = targetDialect,
                DryRun = dryRun,
                CreateBackups = createBackups,
                Verbose = verbose
            };

            if (verbose)
            {
                Console.WriteLine($"Conversion Options: {options}");
                Console.WriteLine($"Scope: {scope}");
            }

            // Execute conversion
            var conversionService = serviceProvider.GetRequiredService<SqlConversionService>();
            var report = await conversionService.ConvertAsync(options);

            // Print report
            PrintReport(report, verbose);

            // Exit code: 0 if successful, 1 if errors
            Environment.Exit(report.ConversionErrors > 0 ? 1 : 0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
                Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses a file path into a ConversionScope.
    /// </summary>
    static ConversionScope ParseConversionScope(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty");

        path = System.IO.Path.GetFullPath(path);

        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            return new ConversionScope.Solution(path);

        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return new ConversionScope.Project(path);

        if (System.IO.Directory.Exists(path))
            return new ConversionScope.Directory(path);

        if (System.IO.File.Exists(path))
            return new ConversionScope.SingleFile(path);

        throw new FileNotFoundException($"Path not found: {path}");
    }

    /// <summary>
    /// Parses target SQL provider string to dialect.
    /// </summary>
    static Dialect.Core.Dialects.ISqlDialect ParseTargetDialect(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" =>
                throw new NotImplementedException("SQL Server dialect registration not yet implemented"),
            "postgresql" or "postgres" =>
                throw new NotImplementedException("PostgreSQL dialect registration not yet implemented"),
            "mysql" =>
                throw new NotImplementedException("MySQL dialect registration not yet implemented"),
            _ => throw new ArgumentException($"Unknown target provider: {providerName}")
        };
    }

    /// <summary>
    /// Prints conversion report to console.
    /// </summary>
    static void PrintReport(ConversionReport report, bool verbose)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   CONVERSION REPORT                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine($"Files Scanned:         {report.TotalFilesScanned}");
        Console.WriteLine($"Files with SQL:        {report.FilesWithSqlFound}");
        Console.WriteLine($"Total SQL Strings:     {report.TotalSqlStringsFound}");
        Console.WriteLine($"Successful:            {report.SuccessfulConversions}");
        Console.WriteLine($"Skipped:               {report.SkippedConversions}");
        Console.WriteLine($"Errors:                {report.ConversionErrors}");
        Console.WriteLine($"Success Rate:          {report.SuccessRate:P}");
        Console.WriteLine();

        if (verbose && report.FileResults.Count > 0)
        {
            Console.WriteLine("File Details:");
            foreach (var fileResult in report.FileResults)
            {
                Console.WriteLine($"  {fileResult.FilePath}");
                foreach (var sqlResult in fileResult.SqlResults)
                {
                    Console.WriteLine($"    Line {sqlResult.LineNumber}: {(sqlResult.IsSuccessful ? "✓" : "✗")} {sqlResult}");
                }
            }
        }

        Console.WriteLine();
    }
}
