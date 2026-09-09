namespace Dialect.Tests.FileRewriting;

using FluentAssertions;
using Xunit;
using System.IO;
using System.Threading.Tasks;
using Dialect.Cli.FileRewriting;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Integration tests for RoslynFileSyntaxRewriter (file-level SQL string translation).
/// Tests C# file rewriting, formatting preservation, and backup/restore functionality.
/// </summary>
public class RoslynFileSyntaxRewriterTests : IAsyncLifetime
{
    private readonly Mock<ISqlTranslator> _translatorMock;
    private readonly Mock<ILogger<RoslynFileSyntaxRewriter>> _loggerMock;
    private readonly RoslynFileSyntaxRewriter _rewriter;
    private string _testDirectory = "";

    public RoslynFileSyntaxRewriterTests()
    {
        _translatorMock = new Mock<ISqlTranslator>();
        _loggerMock = new Mock<ILogger<RoslynFileSyntaxRewriter>>();
        _rewriter = new RoslynFileSyntaxRewriter(_translatorMock.Object, _loggerMock.Object);
    }

    public async Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"dialect_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        await Task.CompletedTask;
    }

    #region Single File Rewriting Tests

    [Fact]
    public async Task RewriteFileAsync_WithValidCsharpAndSql_ReplacesStringsSuccessfully()
    {
        // Arrange
        var csharpContent = @"
var sql = ""SELECT * FROM Users"";
var result = db.ExecuteQuery(sql);
";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        var translatedSql = "SELECT * FROM postgres_users";
        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns(new TranslationResult
            {
                Compiled = new CompiledQuery(translatedSql, new Dictionary<string, object?>())
            });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeTrue();
        result.ReplacedCount.Should().Be(1);
        result.FilePath.Should().Be(testFile);

        var newContent = await File.ReadAllTextAsync(testFile);
        newContent.Should().Contain(translatedSql);
        newContent.Should().NotContain("SELECT * FROM Users");
    }

    [Fact]
    public async Task RewriteFileAsync_WithNoSqlStrings_ReturnsZeroReplacements()
    {
        // Arrange
        var csharpContent = @"
public class UserService
{
    public void DoSomething(string name)
    {
        var greeting = ""Hello, "" + name;
    }
}
";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeTrue();
        result.ReplacedCount.Should().Be(0);
    }

    [Fact]
    public async Task RewriteFileAsync_WithMultipleSqlStrings_ReplacesAllSuccessfully()
    {
        // Arrange
        var csharpContent = @"
var sql1 = ""SELECT * FROM Users"";
var sql2 = ""INSERT INTO Logs VALUES (1, 'test')"";
var sql3 = ""UPDATE Users SET Active = 1"";
";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns<string, SqlProvider, SqlProvider>((sql, src, tgt) => 
                new TranslationResult
                {
                    Compiled = new CompiledQuery($"{sql}_TRANSLATED", new Dictionary<string, object?>())
                });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeTrue();
        result.ReplacedCount.Should().Be(3);
    }

    [Fact]
    public async Task RewriteFileAsync_WithInvalidCsharp_ReturnsError()
    {
        // Arrange
        var invalidCsharp = "this is not valid c# code }{";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, invalidCsharp);

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RewriteFileAsync_WithNonexistentFile_ReturnsError()
    {
        // Act
        var result = await _rewriter.RewriteFileAsync(
            Path.Combine(_testDirectory, "nonexistent.cs"),
            SqlProvider.SqlServer,
            SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Backup and Restore Tests

    [Fact]
    public async Task RewriteFileAsync_WithCreateBackup_CreatesBackupFile()
    {
        // Arrange
        var csharpContent = @"var sql = ""SELECT * FROM Users"";";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns(new TranslationResult
            {
                Compiled = new CompiledQuery("SELECT * FROM new_users", new Dictionary<string, object?>())
            });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql, createBackup: true);

        // Assert
        result.BackupPath.Should().NotBeNullOrEmpty();
        File.Exists(result.BackupPath).Should().BeTrue();
        var backupContent = await File.ReadAllTextAsync(result.BackupPath!);
        backupContent.Should().Contain("SELECT * FROM Users");
    }

    [Fact]
    public async Task RewriteFileAsync_WithoutCreateBackup_DoesNotCreateBackup()
    {
        // Arrange
        var csharpContent = @"var sql = ""SELECT * FROM Users"";";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns(new TranslationResult
            {
                Compiled = new CompiledQuery("SELECT * FROM new_users", new Dictionary<string, object?>())
            });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql, createBackup: false);

        // Assert
        result.BackupPath.Should().BeNullOrEmpty();
        File.Exists(testFile + ".backup").Should().BeFalse();
    }

    [Fact]
    public async Task RestoreFromBackupAsync_WithValidBackup_RestoresOriginalContent()
    {
        // Arrange
        var originalContent = @"var sql = ""SELECT * FROM Users"";";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        var backupFile = testFile + ".backup";
        await File.WriteAllTextAsync(testFile, "modified content");
        await File.WriteAllTextAsync(backupFile, originalContent);

        // Act
        var restored = await _rewriter.RestoreFromBackupAsync(backupFile);

        // Assert
        restored.Should().BeTrue();
        var content = await File.ReadAllTextAsync(testFile);
        content.Should().Be(originalContent);
    }

    [Fact]
    public async Task RestoreFromBackupAsync_WithNonexistentBackup_ReturnsFalse()
    {
        // Act
        var restored = await _rewriter.RestoreFromBackupAsync(Path.Combine(_testDirectory, "nonexistent.backup"));

        // Assert
        restored.Should().BeFalse();
    }

    [Fact]
    public void DeleteBackup_WithValidBackup_DeletesFile()
    {
        // Arrange
        var backupFile = Path.Combine(_testDirectory, "test.cs.backup");
        File.WriteAllText(backupFile, "backup content");

        // Act
        _rewriter.DeleteBackup(backupFile);

        // Assert
        File.Exists(backupFile).Should().BeFalse();
    }

    #endregion

    #region Translation Error Handling Tests

    [Fact]
    public async Task RewriteFileAsync_WithTranslationError_StillSucceedsButRecordsError()
    {
        // Arrange
        var csharpContent = @"
var sql1 = ""SELECT * FROM Users"";
var sql2 = ""INVALID SQL SYNTAX"";
";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        var callCount = 0;
        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns<string, SqlProvider, SqlProvider>((sql, src, tgt) =>
            {
                callCount++;
                if (callCount == 1)
                    return new TranslationResult { Compiled = new CompiledQuery("TRANSLATED", new Dictionary<string, object?>()) };
                return new TranslationResult { ErrorMessage = "Parse error" };
            });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeTrue();
        result.ReplacedCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
    }

    #endregion

    #region Formatting Preservation Tests

    [Fact]
    public async Task RewriteFileAsync_PreservesCodeFormatting()
    {
        // Arrange
        var csharpContent = @"
namespace MyApp
{
    public class UserRepository
    {
        public void FetchUsers()
        {
            const string sql = ""SELECT Id, Name, Email FROM Users WHERE Active = 1"";
            var users = db.Query(sql);
        }
    }
}
";
        var testFile = Path.Combine(_testDirectory, "test.cs");
        await File.WriteAllTextAsync(testFile, csharpContent);

        _translatorMock
            .Setup(t => t.Translate(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>()))
            .Returns(new TranslationResult
            {
                Compiled = new CompiledQuery("SELECT id, name, email FROM users WHERE active = true", new Dictionary<string, object?>())
            });

        // Act
        var result = await _rewriter.RewriteFileAsync(testFile, SqlProvider.SqlServer, SqlProvider.PostgreSql);

        // Assert
        result.Success.Should().BeTrue();
        var newContent = await File.ReadAllTextAsync(testFile);
        
        // Check that structure is preserved
        newContent.Should().Contain("namespace MyApp");
        newContent.Should().Contain("public class UserRepository");
        newContent.Should().Contain("const string sql =");
        newContent.Should().Contain("var users = db.Query(sql);");
    }

    #endregion
}

/// <summary>
/// Tests for BulkFileRewriter (batch processing across multiple files).
/// </summary>
public class BulkFileRewriterTests : IAsyncLifetime
{
    private readonly Mock<ISqlStringReplacer> _replacerMock;
    private readonly Mock<ILogger<BulkFileRewriter>> _loggerMock;
    private readonly BulkFileRewriter _bulkRewriter;
    private string _testDirectory = "";

    public BulkFileRewriterTests()
    {
        _replacerMock = new Mock<ISqlStringReplacer>();
        _loggerMock = new Mock<ILogger<BulkFileRewriter>>();
        _bulkRewriter = new BulkFileRewriter(_replacerMock.Object, _loggerMock.Object);
    }

    public async Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"dialect_bulk_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        await Task.CompletedTask;
    }

    #region File Discovery Tests

    [Fact]
    public async Task RewriteFilesAsync_DiscoversCsharpFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.cs"), "content1");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.cs"), "content2");
        File.WriteAllText(Path.Combine(_testDirectory, "ignore.txt"), "not csharp");

        var options = new FileRewriteOptions { SourceDirectory = _testDirectory };

        _replacerMock
            .Setup(r => r.RewriteFileAsync(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>(), It.IsAny<bool>()))
            .ReturnsAsync(new FileRewriteResult { Success = true, ReplacedCount = 0 });

        // Act
        var result = await _bulkRewriter.RewriteFilesAsync(options);

        // Assert
        result.TotalFilesFound.Should().Be(2);
        _replacerMock.Invocations.Count.Should().Be(2);
    }

    [Fact]
    public async Task RewriteFilesAsync_WithNonexistentDirectory_ReturnsError()
    {
        // Arrange
        var options = new FileRewriteOptions { SourceDirectory = Path.Combine(_testDirectory, "nonexistent") };

        // Act
        var result = await _bulkRewriter.RewriteFilesAsync(options);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Parallel Processing Tests

    [Fact]
    public async Task RewriteFilesAsync_ProcessesFilesInParallel()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_testDirectory, $"file{i}.cs"), $"content{i}");
        }

        var options = new FileRewriteOptions 
        { 
            SourceDirectory = _testDirectory,
            MaxParallelism = 2
        };

        var processedFiles = new List<string>();
        _replacerMock
            .Setup(r => r.RewriteFileAsync(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>(), It.IsAny<bool>()))
            .Returns<string, SqlProvider, SqlProvider, bool>((path, src, tgt, backup) =>
            {
                lock (processedFiles) { processedFiles.Add(path); }
                return Task.FromResult(new FileRewriteResult { Success = true, ReplacedCount = 1 });
            });

        // Act
        var result = await _bulkRewriter.RewriteFilesAsync(options);

        // Assert
        result.FilesProcessed.Should().Be(5);
        result.TotalReplacements.Should().Be(5);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task RewriteFilesAsync_WithContinueOnError_ProcessesAllFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.cs"), "content1");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.cs"), "content2");

        var options = new FileRewriteOptions 
        { 
            SourceDirectory = _testDirectory,
            ContinueOnError = true
        };

        var callCount = 0;
        _replacerMock
            .Setup(r => r.RewriteFileAsync(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>(), It.IsAny<bool>()))
            .Returns<string, SqlProvider, SqlProvider, bool>((path, src, tgt, backup) =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new FileRewriteResult { Success = false, Error = "Translation error" });
                return Task.FromResult(new FileRewriteResult { Success = true, ReplacedCount = 1 });
            });

        // Act
        var result = await _bulkRewriter.RewriteFilesAsync(options);

        // Assert
        result.FilesProcessed.Should().Be(1);
        result.Errors.Should().HaveCount(1);
    }

    #endregion

    #region Aggregation Tests

    [Fact]
    public async Task RewriteFilesAsync_AggregatesResultsCorrectly()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.cs"), "content1");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.cs"), "content2");
        File.WriteAllText(Path.Combine(_testDirectory, "file3.cs"), "content3");

        var options = new FileRewriteOptions { SourceDirectory = _testDirectory };

        _replacerMock
            .Setup(r => r.RewriteFileAsync(It.IsAny<string>(), It.IsAny<SqlProvider>(), It.IsAny<SqlProvider>(), It.IsAny<bool>()))
            .Returns<string, SqlProvider, SqlProvider, bool>((path, src, tgt, backup) =>
            {
                return Task.FromResult(new FileRewriteResult 
                { 
                    Success = true, 
                    ReplacedCount = 3,
                    FilePath = path
                });
            });

        // Act
        var result = await _bulkRewriter.RewriteFilesAsync(options);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalFilesFound.Should().Be(3);
        result.FilesProcessed.Should().Be(3);
        result.TotalReplacements.Should().Be(9);
        result.FileResults.Should().HaveCount(3);
    }

    #endregion
}
