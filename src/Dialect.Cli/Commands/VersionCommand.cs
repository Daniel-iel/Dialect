namespace Dialect.Cli.Commands;

using Microsoft.Extensions.Logging;
using System.Reflection;

/// <summary>
/// Cocona command to display CLI version information.
/// </summary>
public sealed class VersionCommand : ICommand
{
    private readonly ILogger<VersionCommand> _logger;

    public string CommandName => "version";
    public string Description => "Display CLI version information";

    public VersionCommand(ILogger<VersionCommand> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the version command.
    /// </summary>
    public Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            Invoke();
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying version");
            return Task.FromResult(1);
        }
    }

    /// <summary>
    /// Display version information.
    /// </summary>
    private void Invoke()
    {
        var version = GetAssemblyVersion();
        _logger.LogInformation("Dialect.Cli version {Version}", version);
    }

    /// <summary>
    /// Gets the assembly version from metadata.
    /// </summary>
    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (versionAttribute?.InformationalVersion is not null)
            return versionAttribute.InformationalVersion;

        // Fallback to assembly version
        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
