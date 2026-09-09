using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dialect.Cli.Commands;
using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.AST;
using Dialect.Core.DI;
using Dialect.Core.QueryTranslation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dialect.Tests.Cli
{
    /// <summary>
    /// Integration tests for TranslateFilesCommand.
    /// Tests the full workflow: SQL Discovery → File Rewriting → Report Generation.
    /// </summary>
    public sealed class TranslateFilesCommandTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly IServiceProvider _serviceProvider;

        public TranslateFilesCommandTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"dialect_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            // Setup DI
            var services = new ServiceCollection();
            services.AddLogging(logBuilder =>
            {
                logBuilder.AddConsole();
                logBuilder.SetMinimumLevel(LogLevel.Information);
            });
            services.AddSqlTranslation();
            services.AddSingleton<ISqlDiscoveryService, RoslynSqlDiscoveryService>();
            services.AddSingleton<ISqlStringReplacer, RoslynFileSyntaxRewriter>();
            services.AddSingleton<BulkFileRewriter>();
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
            services.AddSingleton<TranslateFilesCommand>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ExecuteAsync_WithValidArguments_SucceedsAndGeneratesReports()
        {
            // Arrange
            var testFile = CreateTestFile("test1.cs", @"
using System;
public class QueryBuilder
{
    public void BuildQuery()
    {
        var query = ""SELECT * FROM users WHERE id = 1"";
        var result = query + "" ORDER BY name"";
    }
}
");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--from", "SqlServer",
                "--to", "PostgreSql",
                "--formats", "json,md,html"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(outputDir), "Output directory should exist");

            var generatedFiles = Directory.GetFiles(outputDir);
            Assert.NotEmpty(generatedFiles);
            Assert.Single(generatedFiles, f => f.EndsWith(".json"));
            Assert.Single(generatedFiles, f => f.EndsWith(".md"));
            Assert.Single(generatedFiles, f => f.EndsWith(".html"));
        }

        [Fact]
        public async Task ExecuteAsync_MissingRequiredArguments_FailsWithError()
        {
            // Arrange
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act - missing --source-dir
            var args = new[] { "--to", "PostgreSql" };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidSourceDirectory_FailsGracefully()
        {
            // Arrange
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", "/nonexistent/directory",
                "--to", "PostgreSql"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithMultipleFiles_ProcessesAll()
        {
            // Arrange
            CreateTestFile("file1.cs", @"
var q1 = ""SELECT * FROM table1"";
");
            CreateTestFile("file2.cs", @"
var q2 = ""SELECT id FROM table2 WHERE status = 1"";
");
            CreateTestFile("subdir/file3.cs", @"
var q3 = ""UPDATE table3 SET active = 0"";
");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--to", "MySql",
                "--patterns", "*.cs"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);

            // Verify reports were generated
            var reports = Directory.GetFiles(outputDir);
            Assert.NotEmpty(reports);

            // Check at least one report exists and contains file information
            Assert.True(reports.Length > 0, "At least one report file should be generated");
        }

        [Fact]
        public async Task ExecuteAsync_ParsesArgumentsCorrectly()
        {
            // Arrange
            var testFile = CreateTestFile("test.cs", @"
var query = ""SELECT 1"";
");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act - with verbose flag
            var args = new[]
            {
                "-s", _tempDir,
                "-o", outputDir,
                "-f", "PostgreSql",
                "-t", "MySql",
                "-p", "*.cs",
                "--verbose"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithInvalidDialect_HandlesGracefully()
        {
            // Arrange
            CreateTestFile("test.cs", "var x = \"SELECT 1\";");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--to", "InvalidDialect"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert - should fail because target dialect is invalid
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task ExecuteAsync_GeneratesValidJsonReport()
        {
            // Arrange
            CreateTestFile("test.cs", @"
public class Queries
{
    public void Execute()
    {
        var q1 = ""SELECT * FROM users"";
        var q2 = ""DELETE FROM logs"";
    }
}
");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--to", "PostgreSql",
                "--formats", "json"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);

            var jsonFile = Directory.GetFiles(outputDir, "*.json").FirstOrDefault();
            Assert.NotNull(jsonFile);

            var jsonContent = File.ReadAllText(jsonFile);
            Assert.Contains("\"sourceDialect\"", jsonContent);
            Assert.Contains("\"targetDialect\"", jsonContent);
            Assert.Contains("\"fileDetails\"", jsonContent);
        }

        [Fact]
        public async Task ExecuteAsync_GeneratesValidMarkdownReport()
        {
            // Arrange
            CreateTestFile("test.cs", "var q = \"SELECT count(*) FROM table1\";");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--to", "PostgreSql",
                "--formats", "md"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);

            var mdFile = Directory.GetFiles(outputDir, "*.md").FirstOrDefault();
            Assert.NotNull(mdFile);

            var mdContent = File.ReadAllText(mdFile);
            Assert.Contains("#", mdContent); // Markdown headers
            Assert.Contains("Total", mdContent); // Summary section
        }

        [Fact]
        public async Task ExecuteAsync_GeneratesValidHtmlReport()
        {
            // Arrange
            CreateTestFile("test.cs", "var q = \"INSERT INTO data VALUES (1, 2, 3)\";");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Act
            var args = new[]
            {
                "--source-dir", _tempDir,
                "--output-dir", outputDir,
                "--to", "MySql",
                "--formats", "html"
            };
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);

            var htmlFile = Directory.GetFiles(outputDir, "*.html").FirstOrDefault();
            Assert.NotNull(htmlFile);

            var htmlContent = File.ReadAllText(htmlFile);
            Assert.Contains("<html", htmlContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("</html>", htmlContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("chart", htmlContent, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Helper to create a test file with content.
        /// </summary>
        private string CreateTestFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(_tempDir, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Cleanup temporary directory after tests.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
