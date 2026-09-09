using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Dialect.Cli.FileRewriting;
using Dialect.Cli.Reporting;

namespace Dialect.Tests.Reporting
{
    public class JsonReportWriterTests : IAsyncLifetime
    {
        private readonly string _testOutputDir;
        private readonly JsonReportWriter _writer;
        private readonly Mock<ILogger<JsonReportWriter>> _loggerMock;

        public JsonReportWriterTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"dialect-json-test-{Guid.NewGuid()}");
            _loggerMock = new Mock<ILogger<JsonReportWriter>>();
            _writer = new JsonReportWriter(_loggerMock.Object);
        }

        public Task InitializeAsync()
        {
            Directory.CreateDirectory(_testOutputDir);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, recursive: true);
            return Task.CompletedTask;
        }

        private BulkFileRewriteResult CreateMockResult(int filesProcessed = 2, int totalFiles = 3, int totalReplacements = 5)
        {
            var fileResults = new List<FileRewriteResult>
            {
                new FileRewriteResult
                {
                    FilePath = "file1.cs",
                    Success = true,
                    ReplacedCount = 2,
                    ReplacedStrings = new List<(string Original, string Translated)>
                    {
                        ("SELECT * FROM users", "SELECT * FROM public.users")
                    }
                },
                new FileRewriteResult
                {
                    FilePath = "file2.cs",
                    Success = true,
                    ReplacedCount = 3
                },
                new FileRewriteResult
                {
                    FilePath = "file3.cs",
                    Success = false,
                    Error = "File not found"
                }
            };

            return new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = filesProcessed,
                TotalFilesFound = totalFiles,
                TotalReplacements = totalReplacements,
                FileResults = fileResults,
                Errors = new List<string> { "File not found: file3.cs" }
            };
        }

        [Fact]
        public async Task WriteReportAsync_WithValidResult_GeneratesJsonFile()
        {
            // Arrange
            var result = CreateMockResult();
            var outputPath = Path.Combine(_testOutputDir, "report.json");

            // Act
            var success = await _writer.WriteReportAsync(result, "Test Report", outputPath);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));
            
            var json = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.Equal("Test Report", root.GetProperty("title").GetString());
            Assert.Equal(3, root.GetProperty("totalFiles").GetInt32());
            Assert.Equal(2, root.GetProperty("successfulFiles").GetInt32());
        }

        [Fact]
        public async Task WriteReportAsync_NullResult_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _writer.WriteReportAsync(null!, "Title", "path.json"));
        }

        [Fact]
        public async Task WriteReportAsync_EmptyOutputPath_ThrowsArgumentException()
        {
            // Arrange
            var result = CreateMockResult();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _writer.WriteReportAsync(result, "Title", ""));
        }

        [Fact]
        public async Task WriteReportAsync_WithNoErrors_OmitsErrorSummaries()
        {
            // Arrange
            var result = new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 2,
                TotalFilesFound = 2,
                TotalReplacements = 5,
                FileResults = new List<FileRewriteResult>(),
                Errors = new List<string>()
            };
            var outputPath = Path.Combine(_testOutputDir, "clean-report.json");

            // Act
            await _writer.WriteReportAsync(result, "Clean Report", outputPath);

            // Assert
            var json = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(json);
            var errorSummaries = doc.RootElement.GetProperty("errorSummaries");
            
            Assert.Empty(errorSummaries.EnumerateArray());
        }

        [Fact]
        public async Task WriteReportAsync_CategorizesErrorsByType()
        {
            // Arrange
            var result = new BulkFileRewriteResult
            {
                Success = false,
                FilesProcessed = 0,
                TotalFilesFound = 3,
                TotalReplacements = 0,
                FileResults = new List<FileRewriteResult>(),
                Errors = new List<string>
                {
                    "File not found: test1.cs",
                    "File not found: test2.cs",
                    "Parse error in test3.cs"
                }
            };
            var outputPath = Path.Combine(_testOutputDir, "categorized-errors.json");

            // Act
            await _writer.WriteReportAsync(result, "Error Report", outputPath);

            // Assert
            var json = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(json);
            var errorSummaries = doc.RootElement.GetProperty("errorSummaries");
            
            var summaryList = errorSummaries.EnumerateArray();
            Assert.NotEmpty(summaryList);
            
            var categories = new HashSet<string>();
            foreach (var summary in summaryList)
            {
                categories.Add(summary.GetProperty("category").GetString()!);
            }
            
            Assert.Contains("File Not Found", categories);
            Assert.Contains("Parse Error", categories);
        }
    }

    public class MarkdownReportWriterTests : IAsyncLifetime
    {
        private readonly string _testOutputDir;
        private readonly MarkdownReportWriter _writer;
        private readonly Mock<ILogger<MarkdownReportWriter>> _loggerMock;

        public MarkdownReportWriterTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"dialect-md-test-{Guid.NewGuid()}");
            _loggerMock = new Mock<ILogger<MarkdownReportWriter>>();
            _writer = new MarkdownReportWriter(_loggerMock.Object);
        }

        public Task InitializeAsync()
        {
            Directory.CreateDirectory(_testOutputDir);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, recursive: true);
            return Task.CompletedTask;
        }

        private BulkFileRewriteResult CreateMockResult()
        {
            return new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 2,
                TotalFilesFound = 3,
                TotalReplacements = 5,
                FileResults = new List<FileRewriteResult>
                {
                    new FileRewriteResult
                    {
                        FilePath = "src/Service.cs",
                        Success = true,
                        ReplacedCount = 3
                    },
                    new FileRewriteResult
                    {
                        FilePath = "src/Repository.cs",
                        Success = true,
                        ReplacedCount = 2
                    },
                    new FileRewriteResult
                    {
                        FilePath = "src/Bad.cs",
                        Success = false,
                        Error = "Syntax error"
                    }
                },
                Errors = new List<string>()
            };
        }

        [Fact]
        public async Task WriteReportAsync_GeneratesMarkdownWithProperStructure()
        {
            // Arrange
            var result = CreateMockResult();
            var outputPath = Path.Combine(_testOutputDir, "report.md");

            // Act
            var success = await _writer.WriteReportAsync(result, "Test Report", outputPath);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));
            
            var content = await File.ReadAllTextAsync(outputPath);
            
            Assert.Contains("# Test Report", content);
            Assert.Contains("## Summary", content);
            Assert.Contains("## File Details", content);
            Assert.Contains("| File | Status | Translations | Error |", content);
            Assert.Contains("✅", content);
            Assert.Contains("❌", content);
        }

        [Fact]
        public async Task WriteReportAsync_IncludesCorrectStatistics()
        {
            // Arrange
            var result = CreateMockResult();
            var outputPath = Path.Combine(_testOutputDir, "stats-report.md");

            // Act
            await _writer.WriteReportAsync(result, "Statistics Report", outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            
            Assert.Contains("Total Files | 3", content);
            Assert.Contains("Successful | 2 ✅", content);
            Assert.Contains("Failed | 1 ❌", content);
            Assert.Contains("Total Translations | 5", content);
        }

        [Fact]
        public async Task WriteReportAsync_EscapesPipeCharactersInFilePaths()
        {
            // Arrange
            var result = new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 1,
                TotalFilesFound = 1,
                TotalReplacements = 1,
                FileResults = new List<FileRewriteResult>
                {
                    new FileRewriteResult
                    {
                        FilePath = "src|weird|path.cs",
                        Success = true,
                        ReplacedCount = 1
                    }
                },
                Errors = new List<string>()
            };
            var outputPath = Path.Combine(_testOutputDir, "escape-test.md");

            // Act
            await _writer.WriteReportAsync(result, "Escape Test", outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("src\\|weird\\|path.cs", content);
        }
    }

    public class HtmlDashboardGeneratorTests : IAsyncLifetime
    {
        private readonly string _testOutputDir;
        private readonly HtmlDashboardGenerator _generator;
        private readonly Mock<ILogger<HtmlDashboardGenerator>> _loggerMock;

        public HtmlDashboardGeneratorTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"dialect-html-test-{Guid.NewGuid()}");
            _loggerMock = new Mock<ILogger<HtmlDashboardGenerator>>();
            _generator = new HtmlDashboardGenerator(_loggerMock.Object);
        }

        public Task InitializeAsync()
        {
            Directory.CreateDirectory(_testOutputDir);
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, recursive: true);
            return Task.CompletedTask;
        }

        private BulkFileRewriteResult CreateMockResult(int successful = 4, int total = 5)
        {
            var fileResults = new List<FileRewriteResult>();
            for (int i = 0; i < total; i++)
            {
                fileResults.Add(new FileRewriteResult
                {
                    FilePath = $"file{i + 1}.cs",
                    Success = i < successful,
                    ReplacedCount = i + 1,
                    Error = i >= successful ? "Test error" : null
                });
            }

            return new BulkFileRewriteResult
            {
                Success = successful == total,
                FilesProcessed = successful,
                TotalFilesFound = total,
                TotalReplacements = fileResults.Count * 2,
                FileResults = fileResults,
                Errors = successful < total ? new List<string> { "Test error" } : new List<string>()
            };
        }

        [Fact]
        public async Task WriteReportAsync_GeneratesValidHtml()
        {
            // Arrange
            var result = CreateMockResult();
            var outputPath = Path.Combine(_testOutputDir, "report.html");

            // Act
            var success = await _generator.WriteReportAsync(result, "Dashboard Test", outputPath);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));
            
            var content = await File.ReadAllTextAsync(outputPath);
            
            Assert.Contains("<!DOCTYPE html>", content);
            Assert.Contains("</html>", content);
            Assert.Contains("<title>Dashboard Test</title>", content);
            Assert.Contains("<script src=\"https://cdn.jsdelivr.net/npm/chart.js", content);
        }

        [Fact]
        public async Task WriteReportAsync_IncludesChartData()
        {
            // Arrange
            var result = CreateMockResult(3, 4);
            var outputPath = Path.Combine(_testOutputDir, "charts.html");

            // Act
            await _generator.WriteReportAsync(result, "Charts", outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            
            Assert.Contains("successChart", content);
            Assert.Contains("translationsChart", content);
            Assert.Contains("new Chart", content);
        }

        [Fact]
        public async Task WriteReportAsync_ContainsSummaryCards()
        {
            // Arrange
            var result = CreateMockResult(3, 4);
            var outputPath = Path.Combine(_testOutputDir, "summary.html");

            // Act
            await _generator.WriteReportAsync(result, "Summary", outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            
            Assert.Contains("summary-cards", content);
            Assert.Contains("Total Files", content);
            Assert.Contains("Successful", content);
            Assert.Contains("Failed", content);
        }

        [Fact]
        public async Task WriteReportAsync_EscapesHtmlEntities()
        {
            // Arrange
            var result = new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 1,
                TotalFilesFound = 1,
                TotalReplacements = 1,
                FileResults = new List<FileRewriteResult>
                {
                    new FileRewriteResult
                    {
                        FilePath = "file<malicious>.cs",
                        Success = true,
                        ReplacedCount = 1,
                        Error = "Test & error"
                    }
                },
                Errors = new List<string>()
            };
            var outputPath = Path.Combine(_testOutputDir, "escape.html");

            // Act
            await _generator.WriteReportAsync(result, "Test<Title>", outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            
            // Verify user-provided content is escaped
            Assert.Contains("&lt;", content); // < escaped
            Assert.Contains("&gt;", content); // > escaped
            Assert.Contains("&amp;", content); // & escaped
            
            // Verify title and path contain escaped entities
            Assert.Contains("Test&lt;Title&gt;", content); // Title properly escaped
            Assert.Contains("&lt;malicious&gt;", content); // File path properly escaped
        }
    }

    public class ReportingServiceTests
    {
        private readonly Mock<ILogger<ReportingService>> _loggerMock;
        private readonly Mock<IReportWriter> _jsonWriterMock;
        private readonly Mock<IReportWriter> _mdWriterMock;

        public ReportingServiceTests()
        {
            _loggerMock = new Mock<ILogger<ReportingService>>();
            _jsonWriterMock = new Mock<IReportWriter>();
            _mdWriterMock = new Mock<IReportWriter>();

            _jsonWriterMock.SetupGet(w => w.FileExtension).Returns("json");
            _mdWriterMock.SetupGet(w => w.FileExtension).Returns("md");
        }

        private BulkFileRewriteResult CreateMockResult()
        {
            return new BulkFileRewriteResult
            {
                Success = true,
                FilesProcessed = 1,
                TotalFilesFound = 1,
                TotalReplacements = 1,
                FileResults = new List<FileRewriteResult>(),
                Errors = new List<string>()
            };
        }

        [Fact]
        public async Task GenerateReportsAsync_CallsAllWritersForSpecifiedFormats()
        {
            // Arrange
            var writers = new Dictionary<string, IReportWriter>
            {
                { "json", _jsonWriterMock.Object },
                { "md", _mdWriterMock.Object }
            };
            var service = new ReportingService(writers, _loggerMock.Object);
            var result = CreateMockResult();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            _jsonWriterMock.Setup(w => w.WriteReportAsync(It.IsAny<BulkFileRewriteResult>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mdWriterMock.Setup(w => w.WriteReportAsync(It.IsAny<BulkFileRewriteResult>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            try
            {
                // Act
                var paths = await service.GenerateReportsAsync(result, tempDir, new[] { "json", "md" }, "Test");

                // Assert
                Assert.NotEmpty(paths);
                _jsonWriterMock.Verify(w => w.WriteReportAsync(It.IsAny<BulkFileRewriteResult>(), "Test", It.IsAny<string>()), Times.Once);
                _mdWriterMock.Verify(w => w.WriteReportAsync(It.IsAny<BulkFileRewriteResult>(), "Test", It.IsAny<string>()), Times.Once);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void AvailableFormats_ReturnsRegisteredFormats()
        {
            // Arrange
            var writers = new Dictionary<string, IReportWriter>
            {
                { "json", _jsonWriterMock.Object },
                { "md", _mdWriterMock.Object }
            };
            var service = new ReportingService(writers, _loggerMock.Object);

            // Act
            var formats = service.AvailableFormats;

            // Assert
            Assert.Contains("json", formats);
            Assert.Contains("md", formats);
            Assert.Equal(2, formats.Count);
        }

        [Fact]
        public async Task GenerateReportsAsync_NullResult_ThrowsArgumentNullException()
        {
            // Arrange
            var writers = new Dictionary<string, IReportWriter>();
            var service = new ReportingService(writers, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.GenerateReportsAsync(null!, "dir"));
        }

        [Fact]
        public async Task GenerateReportsAsync_EmptyOutputDirectory_ThrowsArgumentException()
        {
            // Arrange
            var writers = new Dictionary<string, IReportWriter>();
            var service = new ReportingService(writers, _loggerMock.Object);
            var result = CreateMockResult();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.GenerateReportsAsync(result, ""));
        }
    }

    public class ReportWriterFactoryTests
    {
        private readonly Mock<ILogger<JsonReportWriter>> _jsonLoggerMock;
        private readonly Mock<ILogger<MarkdownReportWriter>> _mdLoggerMock;
        private readonly Mock<ILogger<HtmlDashboardGenerator>> _htmlLoggerMock;

        public ReportWriterFactoryTests()
        {
            _jsonLoggerMock = new Mock<ILogger<JsonReportWriter>>();
            _mdLoggerMock = new Mock<ILogger<MarkdownReportWriter>>();
            _htmlLoggerMock = new Mock<ILogger<HtmlDashboardGenerator>>();
        }

        [Fact]
        public void CreateAll_ReturnsAllThreeWriters()
        {
            // Act
            var writers = ReportWriterFactory.CreateAll(_jsonLoggerMock.Object, _mdLoggerMock.Object, _htmlLoggerMock.Object);

            // Assert
            Assert.NotNull(writers);
            Assert.Contains("json", writers.Keys);
            Assert.Contains("md", writers.Keys);
            Assert.Contains("html", writers.Keys);
            Assert.Equal(3, writers.Count);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("md")]
        [InlineData("html")]
        public void Create_WithValidFormat_ReturnsWriter(string format)
        {
            // Act
            var writer = ReportWriterFactory.Create(format, _jsonLoggerMock.Object, _mdLoggerMock.Object, _htmlLoggerMock.Object);

            // Assert
            Assert.NotNull(writer);
        }

        [Fact]
        public void Create_WithInvalidFormat_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => ReportWriterFactory.Create("invalid", _jsonLoggerMock.Object, _mdLoggerMock.Object, _htmlLoggerMock.Object));
        }
    }
}
