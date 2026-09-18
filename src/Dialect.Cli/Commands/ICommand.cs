namespace Dialect.Cli.Commands;

/// <summary>
/// Strategy interface for CLI commands.
/// Each command implements this contract to provide execution logic.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Gets the command name (e.g., "convert", "version", "config").
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Gets a short description of what the command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command with the provided arguments.
    /// </summary>
    /// <param name="args">Command-specific arguments.</param>
    /// <returns>Exit code (0 = success, 1 = error).</returns>
    Task<int> ExecuteAsync(string[] args);
}
