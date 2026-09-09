namespace Dialect.Cli.Commands;

using Dialect.Cli.Models;
using Dialect.Cli.Services;
using Dialect.Cli.Utilities;
using Dialect.SqlServer;
using Dialect.PostgreSql;
using Dialect.MySql;
using Microsoft.Extensions.Logging;

/// <summary>
/// Command for SQL dialect conversion.
/// Discovers SQL in C# source and converts SQL text to the target dialect.
/// </summary>
public sealed class ConvertCommand : ICommand
{
    private readonly SqlConversionService _conversionService;
    private readonly ILogger<ConvertCommand> _logger;

    public string CommandName => "convert";
    public string Description => "Convert SQL strings to target SQL dialect";

    public ConvertCommand(SqlConversionService conversionService, ILogger<ConvertCommand> logger)
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the convert command with provided arguments.
    /// </summary>
    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            var (path, sourceProvider, connectionString, targetProvider, dryRun, apply, verbose, noBackup)
                = ParseArgs(args);

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogError("Path argument is required for 'convert' command");
                return 1;
            }

            return await InvokeAsync(path, sourceProvider, connectionString, targetProvider, dryRun, apply, verbose, noBackup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in convert command");
            return 1;
        }
    }

    /// <summary>
    /// Execute SQL dialect conversion.
    /// </summary>
    private async Task<int> InvokeAsync(
        string path,
        string? sourceProvider = null,
        string? connectionString = null,
        string targetProvider = "SqlServer",
        bool dryRun = true,
        bool apply = false,
        bool verbose = false,
        bool noBackup = false)
    {
        try
        {
            // --apply overrides --dry-run
            if (apply)
                dryRun = false;

            _logger.LogInformation("Starting SQL-to-FluentBuilder conversion");

            // Parse arguments
            var scope = ParseConversionScope(path);
            var targetDialect = ParseTargetDialect(targetProvider);

            // Build conversion options
            var options = new ConversionOptions
            {
                Scope = scope,
                SourceProvider = sourceProvider is not null
                    ? Enum.Parse<Dialect.Core.AST.SqlProvider>(sourceProvider)
                    : null,
                ConnectionString = connectionString,
                TargetDialect = targetDialect,
                DryRun = dryRun,
                CreateBackups = !noBackup,
                Verbose = verbose
            };

            if (verbose)
            {
                _logger.LogInformation("Conversion Options: {@Options}", options);
                _logger.LogInformation("Scope: {@Scope}", scope);
            }

            // Execute conversion
            var result = await _conversionService.ConvertAsync(options);

            // Handle result
            if (result.IsError)
            {
                _logger.LogError("Conversion failed: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return 1;
            }

            // Render report
            var report = result.Value;
            OutputFormatter.RenderReport(report, _logger, verbose);

            // Exit code: 0 if successful, 1 if errors
            return report.ConversionErrors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during conversion");
            return 1;
        }
    }

    /// <summary>
    /// Parses a file path into a ConversionScope.
    /// </summary>
    private static ConversionScope ParseConversionScope(string path)
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
    private static Dialect.Core.Dialects.ISqlDialect ParseTargetDialect(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" => new SqlServerDialect(),
            "postgresql" or "postgres" => new PostgreSqlDialect(),
            "mysql" => new MySqlDialect(),
            _ => throw new ArgumentException($"Unknown target provider: {providerName}")
        };
    }

    /// <summary>
    /// Parses command-line arguments for the convert command.
    /// </summary>
    private static (string? path, string? sourceProvider, string? connectionString, string targetProvider,
        bool dryRun, bool apply, bool verbose, bool noBackup) ParseArgs(string[] args)
    {
        string? path = null;
        string? sourceProvider = null;
        string? connectionString = null;
        string targetProvider = "SqlServer";
        bool dryRun = true;
        bool apply = false;
        bool verbose = false;
        bool noBackup = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "-sp" or "--source-provider":
                    if (i + 1 < args.Length) sourceProvider = args[++i];
                    break;
                case "-cs" or "--connection-string":
                    if (i + 1 < args.Length) connectionString = args[++i];
                    break;
                case "-tp" or "--target-provider":
                    if (i + 1 < args.Length) targetProvider = args[++i];
                    break;
                case "-d" or "--dry-run":
                    dryRun = true;
                    break;
                case "-a" or "--apply":
                    apply = true;
                    dryRun = false;
                    break;
                case "-v" or "--verbose":
                    verbose = true;
                    break;
                case "--no-backup":
                    noBackup = true;
                    break;
                default:
                    if (!args[i].StartsWith("-")) path = args[i];
                    break;
            }
        }

        return (path, sourceProvider, connectionString, targetProvider, dryRun, apply, verbose, noBackup);
    }
}
