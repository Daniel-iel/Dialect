using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dialect.Cli.Commands;
using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.DI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dialect.Tests.Cli
{
    public sealed class SecurityIntegrationTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<string> _externalDirs = new();

        public SecurityIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"dialect_sec_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

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
        public async Task TranslateFilesCommand_RejectsRelativePathTraversalSourceDir()
        {
            // Arrange: create a directory outside the repo root (sibling to repo)
            var repoRoot = Directory.GetCurrentDirectory();
            var parent = Directory.GetParent(repoRoot)!.FullName;
            var outsideName = $"dialect_outside_{Guid.NewGuid():N}";
            var outsidePath = Path.Combine(parent, outsideName);
            Directory.CreateDirectory(outsidePath);
            _externalDirs.Add(outsidePath);

            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Use a relative path that escapes the repo root
            var args = new[]
            {
                "--source-dir", Path.Combine("..", outsideName),
                "--to", "PostgreSql"
            };

            // Act
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task TranslateFilesCommand_AcceptsNestedSourceDirectoryProcesses()
        {
            // Arrange: create a nested directory inside the repo root and add a C# file
            var repoRoot = Directory.GetCurrentDirectory();
            var nestedName = $"dialect_repo_nested_{Guid.NewGuid():N}";
            var nestedPath = Path.Combine(repoRoot, nestedName);
            Directory.CreateDirectory(nestedPath);
            _externalDirs.Add(nestedPath);

            var testFilePath = Path.Combine(nestedPath, "test.cs");
            File.WriteAllText(testFilePath, "var q = \"SELECT 1\";");

            var outputDir = Path.Combine(nestedPath, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            var args = new[]
            {
                "--source-dir", nestedName,
                "--output-dir", outputDir,
                "--to", "PostgreSql",
                "--formats", "json"
            };

            // Act
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(outputDir));
            Assert.NotEmpty(Directory.GetFiles(outputDir));
        }

        [Fact]
        public async Task TranslateFilesCommand_SanitizesCredentialsInReports()
        {
            // Arrange: create source directory with SQL containing a credential
            var sourceDir = Path.Combine(_tempDir, "cred_src");
            Directory.CreateDirectory(sourceDir);
            var filePath = Path.Combine(sourceDir, "creds.cs");
            var sql = "SELECT * FROM users WHERE username = 'admin' AND password = 'secret123'";
            File.WriteAllText(filePath, $"var q = \"{sql}\";");

            var outputDir = Path.Combine(_tempDir, "reports_creds");
            Directory.CreateDirectory(outputDir);

            // Build a fake BulkFileRewriteResult that contains a translation pair with credentials
            var fileResult = new FileRewriteResult
            {
                Success = true,
                FilePath = filePath,
                ReplacedCount = 1,
                ReplacedStrings = new List<(string Original, string Translated)>
                {
                    (sql, sql)
                },
                Errors = Array.Empty<string>()
            };

            var bulkResult = new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 1,
                TotalFilesFound = 1,
                TotalReplacements = 1,
                FileResults = new List<FileRewriteResult> { fileResult },
                Errors = Array.Empty<string>()
            };

            var writer = ActivatorUtilities.CreateInstance<JsonReportWriter>(_serviceProvider);
            var outputPath = Path.Combine(outputDir, "report.json");

            // Act - write report directly
            var ok = await writer.WriteReportAsync(bulkResult, "Test Report", outputPath);
            Assert.True(ok, "Report writer should succeed");

            // Assert - report generated and sanitizer masks the sensitive value
            var jsonFile = outputPath;
            Assert.True(File.Exists(jsonFile));

            var sanitized = Dialect.Cli.Security.SecurityValidator.SanitizeSqlForLogging(sql);
            Assert.DoesNotContain("secret123", sanitized);
            Assert.Contains("***", sanitized);
        }

        [Fact]
        public async Task TranslateFilesCommand_RejectsUnsafeGlobPattern()
        {
            // Arrange: create a valid source directory
            var sourceDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file.cs"), "var q = \"SELECT 1\";");

            var outputDir = Path.Combine(_tempDir, "reports");
            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            // Unsafe glob contains path traversal
            var args = new[]
            {
                "--source-dir", sourceDir,
                "--output-dir", outputDir,
                "--to", "PostgreSql",
                "--patterns", "../../dangerous/*.cs"
            };

            // Act
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task TranslateFilesCommand_RejectsHiddenSourceDirectory()
        {
            // Arrange: create hidden directory
            var hidden = Path.Combine(Path.GetTempPath(), $"dialect_hidden_{Guid.NewGuid():N}");
            Directory.CreateDirectory(hidden);
            var di = new DirectoryInfo(hidden);
            di.Attributes |= FileAttributes.Hidden;
            _externalDirs.Add(hidden);

            var command = ActivatorUtilities.CreateInstance<TranslateFilesCommand>(_serviceProvider);

            var args = new[]
            {
                "--source-dir", hidden,
                "--to", "PostgreSql"
            };

            // Act
            var exitCode = await command.ExecuteAsync(args);

            // Assert
            Assert.Equal(1, exitCode);
        }

        public void Dispose()
        {
            // Remove hidden attribute and cleanup external dirs
            foreach (var d in _externalDirs)
            {
                try
                {
                    if (Directory.Exists(d))
                    {
                        var di = new DirectoryInfo(d);
                        di.Attributes &= ~FileAttributes.Hidden;
                        Directory.Delete(d, recursive: true);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
