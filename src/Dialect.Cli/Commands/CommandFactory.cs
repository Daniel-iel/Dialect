namespace Dialect.Cli.Commands;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Factory for resolving and instantiating CLI commands.
/// Implements the Strategy Pattern to manage different command types.
/// </summary>
public sealed class CommandFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _commandRegistry;

    public CommandFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _commandRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "convert", typeof(ConvertCommand) },
            { "version", typeof(VersionCommand) },
            { "config", typeof(ConfigCommand) }
        };
    }

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IEnumerable<(string Name, string Description)> GetAvailableCommands()
    {
        foreach (var (name, type) in _commandRegistry)
        {
            if (ActivatorUtilities.CreateInstance(_serviceProvider, type) is ICommand cmd)
            {
                yield return (cmd.CommandName, cmd.Description);
            }
        }
    }

    /// <summary>
    /// Resolves a command by name and creates an instance.
    /// Returns null if command not found.
    /// </summary>
    public ICommand? ResolveCommand(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return null;

        if (_commandRegistry.TryGetValue(commandName, out var commandType))
        {
            try
            {
                return ActivatorUtilities.CreateInstance(_serviceProvider, commandType) as ICommand;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a command is registered.
    /// </summary>
    public bool CommandExists(string commandName)
    {
        return _commandRegistry.ContainsKey(commandName ?? string.Empty);
    }
}
