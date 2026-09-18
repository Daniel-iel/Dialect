using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;

namespace Dialect.Cli.Examples
{
    /// <summary>
    /// Example demonstrating how to use the Reporting infrastructure.
    /// This shows practical usage patterns for Phase 3 components.
    /// </summary>
    public class ReportingIntegrationExample
    {
        public static async Task RunAsync()
        {
            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddScoped<JsonReportWriter>();
            services.AddScoped<MarkdownReportWriter>();
            services.AddScoped<HtmlDashboardGenerator>();

            var provider = services.BuildServiceProvider();

            // Get loggers for each writer
            var jsonLogger = provider.GetRequiredService<ILogger<JsonReportWriter>>();
            var mdLogger = provider.GetRequiredService<ILogger<MarkdownReportWriter>>();
            var htmlLogger = provider.GetRequiredService<ILogger<HtmlDashboardGenerator>>();

            // Create all report writers
            var writers = ReportWriterFactory.CreateAll(jsonLogger, mdLogger, htmlLogger);

            // Create reporting service
            var logger = provider.GetRequiredService<ILogger<ReportingService>>();
            var reportingService = new ReportingService(writers, logger);

            // Example bulk file rewrite result
            var result = CreateExampleResult();

            // Generate reports
            var outputDir = Path.Combine(Path.GetTempPath(), "dialect-reports");
            Directory.CreateDirectory(outputDir);

            Console.WriteLine($"📊 Generating reports to: {outputDir}");

            var reportPaths = await reportingService.GenerateReportsAsync(
                result,
                outputDir,
                reportFormats: new[] { "json", "md", "html" },
                title: "SQL Translation Report - Demo"
            );

            Console.WriteLine($"\n✅ Generated {reportPaths.Count} reports:");
            foreach (var path in reportPaths)
            {
                Console.WriteLine($"   📄 {Path.GetFileName(path)}");
            }

            // Display available formats
            Console.WriteLine($"\n📋 Available Report Formats: {string.Join(", ", reportingService.AvailableFormats)}");
        }

        private static BulkFileRewriteResult CreateExampleResult()
        {
            return new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 3,
                TotalFilesFound = 4,
                TotalReplacements = 12,
                FileResults = new List<FileRewriteResult>
                {
                    new FileRewriteResult
                    {
                        FilePath = "Services/UserService.cs",
                        Success = true,
                        ReplacedCount = 4,
                        BackupPath = "Services/UserService.cs.backup",
                        ReplacedStrings = new List<(string, string)>
                        {
                            ("SELECT * FROM [users]", "SELECT * FROM public.users"),
                            ("SELECT TOP 10", "SELECT * LIMIT 10"),
                            ("WHERE id = @id", "WHERE id = $1"),
                            ("ORDER BY name DESC", "ORDER BY name DESC")
                        }
                    },
                    new FileRewriteResult
                    {
                        FilePath = "Repositories/OrderRepository.cs",
                        Success = true,
                        ReplacedCount = 5,
                        BackupPath = "Repositories/OrderRepository.cs.backup"
                    },
                    new FileRewriteResult
                    {
                        FilePath = "Controllers/ReportController.cs",
                        Success = true,
                        ReplacedCount = 3
                    },
                    new FileRewriteResult
                    {
                        FilePath = "Jobs/SyncJob.cs",
                        Success = false,
                        Error = "Complex dynamic SQL detected - manual review required",
                        ReplacedCount = 0
                    }
                },
                Errors = new List<string>
                {
                    "Complex dynamic SQL detected - manual review required: Jobs/SyncJob.cs"
                }
            };
        }
    }
}
