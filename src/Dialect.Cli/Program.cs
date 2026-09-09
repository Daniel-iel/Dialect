using Dialect.Cli.CodeGeneration;
using Dialect.Cli.Commands;
using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;
using Dialect.Cli.Services;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.DI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dialect.Cli;

/// <summary>
/// SQL-to-FluentBuilder C# converter CLI.
/// Uses Strategy Pattern (ICommand) to manage extensible command execution.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();

        // Register logging
        services.AddLogging(logBuilder =>
        {
            logBuilder.AddConsole();
            logBuilder.SetMinimumLevel(LogLevel.Information);
        });

        // Register SQL translation framework
        services.AddSqlTranslation();

        // Register CLI services
        services.AddSingleton<ISqlDiscoveryService, RoslynSqlDiscoveryService>();
        services.AddSingleton<IFluentCodeGenerator, DefaultFluentCodeGenerator>();
        services.AddSingleton<SqlConversionService>();

        // Register file rewriting services
        services.AddSingleton<ISqlStringReplacer, RoslynFileSyntaxRewriter>();
        services.AddSingleton<BulkFileRewriter>();

        // Register reporting services
        services.AddSingleton<JsonReportWriter>();
        services.AddSingleton<MarkdownReportWriter>();
        services.AddSingleton<HtmlDashboardGenerator>();
        services.AddSingleton<ReportingService>(sp =>
        {
            var writers = new Dictionary<string, IReportWriter>
            {
                { "json", sp.GetRequiredService<JsonReportWriter>() },
                { "md", sp.GetRequiredService<MarkdownReportWriter>() },
                { "html", sp.GetRequiredService<HtmlDashboardGenerator>() }
            };
            var logger = sp.GetRequiredService<ILogger<ReportingService>>();
            return new ReportingService(writers, logger);
        });

        // Register command infrastructure (Strategy Pattern)
        services.AddSingleton<CommandFactory>();
        services.AddSingleton<CommandDispatcher>();

        var serviceProvider = services.BuildServiceProvider();

        // Dispatch command execution
        var dispatcher = serviceProvider.GetRequiredService<CommandDispatcher>();
        return await dispatcher.DispatchAsync(args);
    }
}

