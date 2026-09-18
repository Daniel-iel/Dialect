namespace Dialect.Tests.Cli;

using Dialect.Cli.SqlDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;

/// <summary>
/// Phase 7 Part 2: Stress and resilience tests.
/// Tests system behavior under extreme conditions (large files, many operations, edge cases).
/// </summary>
public class StressResilienceTests
{
    private readonly RoslynSqlDiscoveryService _discoveryService;

    public StressResilienceTests()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILogger<RoslynSqlDiscoveryService>>();
        _discoveryService = new RoslynSqlDiscoveryService(logger);
    }

    #region Large File Stress Tests

    [Fact]
    public void StressTest_VeryLargeFile_ProcessesWithoutCrash()
    {
        // Arrange - Generate a 1MB file with many queries
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 2000; i++)
        {
            sb.AppendLine($"var query{i} = @\"SELECT * FROM Table{i} WHERE Id = {i}\";");
            sb.AppendLine($"var insert{i} = @\"INSERT INTO Table{i} VALUES ({i}, 'test')\";");
            sb.AppendLine($"var update{i} = @\"UPDATE Table{i} SET Value = {i} WHERE Id = {i}\";");
        }
        var sourceCode = sb.ToString();
        
        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 4000, $"Expected at least 4000 queries, found {results.Count}");
        Assert.True(sw.ElapsedMilliseconds < 30000, $"Large file processing took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void StressTest_ManyConsecutiveQueries_HandlesCorrectly()
    {
        // Arrange - 500 queries in a row
        var queries = new List<string>();
        for (int i = 0; i < 500; i++)
        {
            queries.Add($"var q{i} = @\"SELECT {i} FROM T{i}\";");
        }
        var sourceCode = string.Join("\n", queries);

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.True(results.Count >= 400, $"Should find most queries, found {results.Count}");
    }

    #endregion

    #region Malformed/Corrupted SQL Handling

    [Fact]
    public void StressTest_WithMalformedSql_HandlesGracefully()
    {
        // Arrange
        var sourceCode = @"
var q1 = @""SELECT * FROM"";  // Incomplete
var q2 = @""SELECT * FROM WHERE"";  // Invalid
var q3 = @""SELECT FROM Users"";  // Wrong order
var q4 = @""SELECT @* FROM Users"";  // Invalid syntax
";

        // Act - Should not crash even with malformed SQL
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        // Should discover some queries despite malformations
        Assert.NotNull(results);
    }

    [Fact]
    public void StressTest_WithVeryLongQueryLine_HandlesWithoutStackOverflow()
    {
        // Arrange - Single line query 50KB long
        var longQuery = "SELECT " + string.Join(", ", Enumerable.Range(1, 5000).Select(i => $"col{i}")) + " FROM Users";
        var sourceCode = $@"var sql = @""{longQuery}"";";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
    }

    #endregion

    #region Mixed Content Resilience

    [Fact]
    public void StressTest_WithMixedCodeAndComments_Handles()
    {
        // Arrange
        var line1 = "// This is a comment with SQL: SELECT * FROM Users";
        var line2 = "var sql = @\"SELECT * FROM Orders\";  // Actual SQL";
        var line3 = "string BuildQuery() { var query = \"SELECT * FROM Items\"; return query; }";
        var line4 = "var finalSql = @\"DELETE FROM Logs\";";
        var sourceCode = string.Join("\n", line1, line2, line3, line4);

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        // Should find actual SQL statements, may or may not find commented ones
        Assert.NotEmpty(results);
    }

    [Fact]
    public void StressTest_WithEscapedQuotesInSql_HandlesCorrectly()
    {
        // Arrange
        var sql1 = "var s1 = @\"SELECT * FROM Users WHERE Name = 'O''Brien'\";";
        var sql2 = "var s2 = @\"SELECT * FROM Users WHERE Name = 'Test'\";";
        var sql3 = "var s3 = \"SELECT * FROM Users WHERE Comment = 'Works'\";";
        var sourceCode = string.Join("\n", sql1, sql2, sql3);

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.True(results.Count >= 2, $"Should find most queries, found {results.Count}");
    }

    #endregion

    #region Memory Efficiency Under Load

    [Fact]
    public void StressTest_RepeatedProcessing_MaintainsMemory()
    {
        // Arrange
        var sourceCode = @"var sql = @""SELECT * FROM Users WHERE Id = 1"";";
        var iterations = 1000;

        // Act - Process same content many times
        for (int i = 0; i < iterations; i++)
        {
            var results = _discoveryService.DiscoverSqlStrings(sourceCode);
            Assert.NotEmpty(results);
        }

        // Assert - If we got here without OutOfMemoryException, we passed
        Assert.True(true, "Completed repeated processing without memory issues");
    }

    [Fact]
    public void StressTest_DeepNesting_HandlesRecursively()
    {
        // Arrange - Deeply nested subqueries
        var sourceCode = @"
var sql = @""
SELECT * FROM (
    SELECT * FROM (
        SELECT * FROM (
            SELECT * FROM (
                SELECT * FROM (
                    SELECT * FROM (
                        SELECT * FROM (
                            SELECT * FROM (
                                SELECT * FROM (
                                    SELECT * FROM (
                                        SELECT UserId FROM Users
                                    ) u1
                                ) u2
                            ) u3
                        ) u4
                    ) u5
                ) u6
            ) u7
        ) u8
    ) u9
) u10
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
    }

    #endregion

    #region Unicode and Special Characters

    [Fact]
    public void StressTest_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"
var sql1 = @""SELECT * FROM Usuários WHERE Nome = 'José'"";
var sql2 = @""SELECT * FROM 用户 WHERE 名称 = '中文'"";
var sql3 = @""SELECT * FROM Users WHERE Comment = 'Ñoño™'"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.True(results.Count >= 2, $"Should handle unicode, found {results.Count}");
    }

    [Fact]
    public void StressTest_WithSpecialSqlCharacters_HandlesCorrectly()
    {
        // Arrange
        var sourceCode = @"
var sql1 = @""SELECT * FROM Users WHERE Email LIKE '%@%.com'"";
var sql2 = @""SELECT CONVERT(DATETIME, '2026-09-08T14:30:00Z') FROM Users"";
var sql3 = @""SELECT * FROM Users WHERE Salary > $100000"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.True(results.Count >= 2, $"Should handle special chars, found {results.Count}");
    }

    #endregion

    #region Error Recovery

    [Fact]
    public void StressTest_WithNullOrEmptyInput_ReturnsEmpty()
    {
        // Act & Assert
        var result1 = _discoveryService.DiscoverSqlStrings(null);
        var result2 = _discoveryService.DiscoverSqlStrings("");
        var result3 = _discoveryService.DiscoverSqlStrings("   ");

        Assert.Empty(result1);
        Assert.Empty(result2);
        Assert.Empty(result3);
    }

    [Fact]
    public void StressTest_WithInvalidCSharp_HandlesParse()
    {
        // Arrange - Invalid C# syntax
        var sourceCode = @"
this is definitely not valid C# code!!!
@#$%^&*()
var sql = @""SELECT * FROM Users"";  // This might still be found
completely broken { ] [ ) } syntax
";

        // Act - Should not crash
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert - May find some SQL depending on parser resilience
        Assert.NotNull(results);
    }

    #endregion

    #region Boundary Conditions

    [Fact]
    public void StressTest_WithMinimalSql_Detects()
    {
        // Arrange
        var sourceCode = @"var sql = ""SELECT a FROM b"";";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
    }

    [Fact]
    public void StressTest_WithMinimalKeyword_HasReasrasonableConfidence()
    {
        // Arrange
        var sql = "SELECT x FROM y";

        // Act
        var score = _discoveryService.IsSuspiciouslyLikesSql(sql);

        // Assert
        Assert.True(score >= 0.5, $"Minimal SQL should have reasonable confidence, got {score}");
    }

    [Fact]
    public void StressTest_WithOneLiners_AllParsed()
    {
        // Arrange - Multiple SQL types on single line
        var sourceCode = @"
var a = @""SELECT * FROM A""; var b = @""INSERT INTO B VALUES(1)""; var c = @""UPDATE C SET x=1"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.True(results.Count >= 2, $"Should find multiple SQLs on one line, found {results.Count}");
    }

    #endregion

    #region Performance Under Stress

    [Fact]
    public void StressTest_Throughput_UnderLoad()
    {
        // Arrange - Many medium-sized files
        var files = new List<string>();
        for (int f = 0; f < 50; f++)
        {
            var sb = new System.Text.StringBuilder();
            for (int q = 0; q < 20; q++)
            {
                sb.AppendLine($"var sql = @\"SELECT * FROM T{f}_{q}\";");
            }
            files.Add(sb.ToString());
        }

        var sw = Stopwatch.StartNew();

        // Act
        var totalResults = 0;
        foreach (var file in files)
        {
            totalResults += _discoveryService.DiscoverSqlStrings(file).Count;
        }

        sw.Stop();

        // Assert
        Assert.True(totalResults >= 800, $"Expected at least 800 queries, found {totalResults}");
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Stress test took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}
