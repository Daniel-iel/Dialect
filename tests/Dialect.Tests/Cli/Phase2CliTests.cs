namespace Dialect.Tests.Cli;

using Dialect.Cli.Models;
using Dialect.Cli.SqlDiscovery;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Tests for Roslyn-based SQL string discovery in C# source code.
/// </summary>
public class RoslynSqlDiscoveryServiceTests
{
    private readonly RoslynSqlDiscoveryService _discoveryService;
    private readonly Mock<ILogger<RoslynSqlDiscoveryService>> _mockLogger;

    public RoslynSqlDiscoveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<RoslynSqlDiscoveryService>>();
        _discoveryService = new RoslynSqlDiscoveryService(_mockLogger.Object);
    }

    [Fact]
    public void DiscoverSqlStrings_WithSimpleSelectStatement_FindsIt()
    {
        // Arrange
        const string sourceCode = @"
            var sql = ""SELECT id, name FROM users WHERE id = @id"";
            var result = sql.Execute();
        ";

        // Act
        var discovered = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        discovered.Should().HaveCount(1);
        discovered[0].SqlContent.Should().Contain("SELECT");
        discovered[0].SqlContent.Should().Contain("FROM users");
        discovered[0].SuspiciouslyLikesSql.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void DiscoverSqlStrings_WithMultipleSqlStrings_FindsAll()
    {
        // Arrange
        const string sourceCode = @"
            var select = ""SELECT * FROM products WHERE price > 100"";
            var insert = ""INSERT INTO orders (customer_id) VALUES (@customerId)"";
            var update = ""UPDATE inventory SET stock = stock - 1 WHERE id = @id"";
            var notSql = ""This is just a regular string"";
        ";

        // Act
        var discovered = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        discovered.Should().HaveCount(3); // Only SQL strings, not the regular string
        discovered.Should().AllSatisfy(x => x.SuspiciouslyLikesSql.Should().BeGreaterThan(0.1));
    }

    [Fact]
    public void DiscoverSqlStrings_WithEmptyCode_ReturnsEmpty()
    {
        // Arrange
        const string sourceCode = "";

        // Act
        var discovered = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        discovered.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverSqlStrings_WithNoSqlStrings_ReturnsEmpty()
    {
        // Arrange
        const string sourceCode = @"
            var greeting = ""Hello World"";
            var message = ""This is not SQL"";
            var count = 42;
        ";

        // Act
        var discovered = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        discovered.Should().BeEmpty();
    }

    [Fact(Skip = "SQL string detection confidence scoring not fully implemented")]
    public void IsSuspiciouslyLikesSql_WithVaryingStrings_ReturnsCorrectConfidence()
    {
        // Test skipped - feature not fully implemented
    }

    [Fact]
    public void IsSuspiciouslyLikesSql_WithSqlKeywords_HigherScore()
    {
        // Act
        var scoreSelect = _discoveryService.IsSuspiciouslyLikesSql("SELECT * FROM table");
        var scoreMultiKeyword = _discoveryService.IsSuspiciouslyLikesSql("SELECT * FROM users WHERE id = 1 ORDER BY name");

        // Assert
        scoreMultiKeyword.Should().BeGreaterThan(scoreSelect);
    }
}

/// <summary>
/// Tests for ConversionScope discriminated union.
/// </summary>
public class ConversionScopeTests
{
    [Fact]
    public void SingleFile_CreatesCorrectScope()
    {
        // Act
        var scope = new ConversionScope.SingleFile("/path/to/file.cs");

        // Assert
        scope.Should().BeOfType<ConversionScope.SingleFile>();
        scope.ToString().Should().Contain("file.cs");
    }

    [Fact]
    public void Directory_CreatesCorrectScope()
    {
        // Act
        var scope = new ConversionScope.Directory("/path/to/dir");

        // Assert
        scope.Should().BeOfType<ConversionScope.Directory>();
        scope.ToString().Should().Contain("Directory");
    }

    [Fact]
    public void Project_CreatesCorrectScope()
    {
        // Act
        var scope = new ConversionScope.Project("/path/to/project.csproj");

        // Assert
        scope.Should().BeOfType<ConversionScope.Project>();
        scope.ToString().Should().Contain("Project");
    }

    [Fact]
    public void Solution_CreatesCorrectScope()
    {
        // Act
        var scope = new ConversionScope.Solution("/path/to/solution.sln");

        // Assert
        scope.Should().BeOfType<ConversionScope.Solution>();
        scope.ToString().Should().Contain("Solution");
    }
}

/// <summary>
/// Tests for ConversionReport statistics and aggregation.
/// </summary>
public class ConversionReportTests
{
    [Fact]
    public void SuccessRate_WithAllSuccessful_ReturnsOne()
    {
        // Arrange
        var report = new ConversionReport
        {
            TotalSqlStringsFound = 5,
            SuccessfulConversions = 5,
            SkippedConversions = 0,
            ConversionErrors = 0
        };

        // Act & Assert
        report.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_WithPartialSuccess_ReturnsCorrectRatio()
    {
        // Arrange
        var report = new ConversionReport
        {
            TotalSqlStringsFound = 10,
            SuccessfulConversions = 7,
            SkippedConversions = 3,
            ConversionErrors = 0
        };

        // Act & Assert
        report.SuccessRate.Should().Be(0.7);
    }

    [Fact]
    public void HasModifications_WithSuccessfulConversions_ReturnsTrue()
    {
        // Arrange
        var report = new ConversionReport
        {
            SuccessfulConversions = 5
        };

        // Act & Assert
        report.HasModifications.Should().BeTrue();
    }

    [Fact]
    public void HasModifications_WithNoSuccessfulConversions_ReturnsFalse()
    {
        // Arrange
        var report = new ConversionReport
        {
            SuccessfulConversions = 0,
            SkippedConversions = 5
        };

        // Act & Assert
        report.HasModifications.Should().BeFalse();
    }

    [Fact]
    public void ToString_IncludesAllMetrics()
    {
        // Arrange
        var report = new ConversionReport
        {
            TotalFilesScanned = 10,
            FilesWithSqlFound = 5,
            TotalSqlStringsFound = 12,
            SuccessfulConversions = 10,
            SkippedConversions = 2,
            ConversionErrors = 0
        };

        // Act & Assert
        var reportStr = report.ToString();
        reportStr.Should().Contain("10 files scanned");
        reportStr.Should().Contain("12 SQL strings found");
        reportStr.Should().Contain("10 converted");
    }
}
