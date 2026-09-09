namespace Dialect.Cli.Commands;

using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates command execution with help/error handling.
/// Uses the Strategy pattern via ICommand interface to execute commands.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly CommandFactory _factory;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(CommandFactory factory, ILogger<CommandDispatcher> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Dispatches command execution or displays help.
    /// </summary>
    public async Task<int> DispatchAsync(string[] args)
    {
        try
        {
            // Show help for empty args or --help flag
            if (args.Length == 0 || IsHelpRequested(args[0]))
            {
                DisplayHelp();
                return 0;
            }

            var commandName = args[0];
            var commandArgs = args.Skip(1).ToArray();

            // Resolve command from factory
            var command = _factory.ResolveCommand(commandName);
            if (command == null)
            {
                _logger.LogError("Unknown command: {CommandName}", commandName);
                DisplayHelp();
                return 1;
            }

            // Execute command with remaining arguments
            return await command.ExecuteAsync(commandArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in command dispatcher");
            return 1;
        }
    }

    /// <summary>
    /// Displays help text with available commands and usage.
    /// </summary>
    private void DisplayHelp()
    {
        var commands = _factory.GetAvailableCommands().ToList();

        _logger.LogInformation(@"
Dialect CLI v1.0.0 - SQL to FluentBuilder Converter

Usage: dialect-cli <command> [options]

Available Commands:");

        foreach (var (name, description) in commands)
        {
            _logger.LogInformation("  {CommandName,-12} {Description}", name, description);
        }

        _logger.LogInformation(@"
Global Options:
  -h, --help                        Show this help message
  -v, --verbose                     Enable verbose logging

Convert Command Options:
  -sp, --source-provider <provider> Source SQL provider (SqlServer|PostgreSql|MySql)
  -cs, --connection-string <string> Connection string for provider auto-detection
  -tp, --target-provider <provider> Target SQL dialect (default: SqlServer)
  -d, --dry-run                     Preview mode; don't modify files (default)
  -a, --apply                       Apply conversions and modify files
  -v, --verbose                     Enable detailed diagnostic logging
  --no-backup                       Skip backup file creation

Examples:
  dialect-cli convert ./src -v
  dialect-cli convert ./file.cs --apply --verbose
  dialect-cli version
  dialect-cli config show
");
    }

    /// <summary>
    /// Checks if help is requested via --help or -h flag.
    /// </summary>
    private static bool IsHelpRequested(string arg)
    {
        return arg == "--help" || arg == "-h" || arg == "help";
    }
}
