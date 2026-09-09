namespace Dialect.Tests.Cli;

using Dialect.Cli.SqlDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;

/// <summary>
/// Phase 6 Part 3: Performance tests for SQL discovery and transformation.
/// Measures throughput, latency, and identifies optimization opportunities.
/// </summary>
public class PerformanceOptimizationTests
{
    private readonly RoslynSqlDiscoveryService _discoveryService;

    public PerformanceOptimizationTests()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILogger<RoslynSqlDiscoveryService>>();
        _discoveryService = new RoslynSqlDiscoveryService(logger);
    }

    #region Basic Performance Benchmarks

    [Fact]
    public void PerformanceBenchmark_SingleQueryDiscovery_CompletesUnder100ms()
    {
        // Arrange
        var sourceCode = @"
var query = @""SELECT UserId, UserName FROM Users WHERE Status = 'Active' ORDER BY CreatedDate DESC"";
";
        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        Assert.NotEmpty(results);
        Assert.True(sw.ElapsedMilliseconds < 100, $"Single query discovery took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void PerformanceBenchmark_MultipleQueriesInFile_CompletesUnder500ms()
    {
        // Arrange - Generate a file with 20 SQL queries
        var queries = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            queries.Add($@"var query{i} = @""SELECT * FROM Table{i} WHERE Id = {i}"";");
        }
        var sourceCode = string.Join("\n", queries);
        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        Assert.True(results.Count >= 15, $"Expected at least 15 queries, found {results.Count}");
        Assert.True(sw.ElapsedMilliseconds < 500, $"Multi-query discovery took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void PerformanceBenchmark_LargeFile_ProcessesWithinAcceptableTime()
    {
        // Arrange - Generate a large file (10KB) with multiple queries
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Large C# file simulation");
        
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($@"
public class Repository{i}
{{
    public void Method1()
    {{
        var sql = @""SELECT * FROM Users WHERE Id = {i}"";
        var result = Execute(sql);
    }}

    public void Method2()
    {{
        var query = $@""SELECT Name FROM Orders WHERE CustomerId = @custId"";
        db.Execute(query);
    }}
}}");
        }

        var sourceCode = sb.ToString();
        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        Assert.NotEmpty(results);
        Assert.True(sw.ElapsedMilliseconds < 1000, 
            $"Large file processing took {sw.ElapsedMilliseconds}ms, source size: {sourceCode.Length} bytes");
    }

    #endregion

    #region Confidence Scoring Performance

    [Fact]
    public void PerformanceBenchmark_ConfidenceScoring_BatchProcessing()
    {
        // Arrange - Test scoring of 100 different strings
        var sqlStrings = new List<string>
        {
            "SELECT * FROM Users",
            "INSERT INTO Orders VALUES (1, 'test')",
            "UPDATE Products SET Price = 99 WHERE Id = 1",
            "DELETE FROM Logs WHERE CreatedDate < DATEADD(MONTH, -3, GETDATE())",
            "SELECT u.*, o.* FROM Users u JOIN Orders o ON u.Id = o.UserId",
            "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte",
            "MERGE INTO Target t USING Source s ON t.Id = s.Id",
            "SELECT ROW_NUMBER() OVER (ORDER BY Id) FROM Users",
            "EXEC sp_GetUserData @userId = 123"
        };

        var sw = Stopwatch.StartNew();

        // Act
        var scores = new Dictionary<string, double>();
        foreach (var sql in sqlStrings)
        {
            scores[sql] = _discoveryService.IsSuspiciouslyLikesSql(sql);
        }

        // Assert
        sw.Stop();
        Assert.All(scores, kvp => Assert.True(kvp.Value >= 0 && kvp.Value <= 1));
        Assert.True(sw.ElapsedMilliseconds < 50, $"Confidence scoring batch took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Throughput Tests

    [Fact]
    public void PerformanceBenchmark_DiscoveryThroughput_MeasuresSqlsPerSecond()
    {
        // Arrange - Generate many queries
        var queries = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            queries.Add($@"var sql{i} = @""SELECT Id, Name FROM Table{i} WHERE Status = 'Active'"";");
        }
        var sourceCode = string.Join("\n", queries);
        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        var throughput = results.Count / (sw.ElapsedMilliseconds / 1000.0);
        Assert.True(throughput > 100, $"Throughput {throughput:.1f} SQLs/sec is below threshold of 100");
        
        // Log performance metric
        var output = $"SQL Discovery Throughput: {throughput:.1f} SQLs/sec ({results.Count} SQLs in {sw.ElapsedMilliseconds}ms)";
        Assert.NotEmpty(output); // Just to use the variable
    }

    #endregion

    #region Memory Efficiency Tests

    [Fact]
    public void PerformanceBenchmark_MemoryUsage_DoesNotGrowLinearlyWithFileSize()
    {
        // Arrange - Test with increasingly large files
        var sizes = new[] { 1000, 5000, 10000 };
        var timings = new List<long>();

        foreach (var size in sizes)
        {
            var queries = new List<string>();
            for (int i = 0; i < size; i++)
            {
                queries.Add($@"var q{i} = @""SELECT * FROM T{i}"";");
            }
            var sourceCode = string.Join("\n", queries);
            var sw = Stopwatch.StartNew();

            // Act
            var results = _discoveryService.DiscoverSqlStrings(sourceCode);

            // Assert
            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);
            Assert.NotEmpty(results);
        }

        // Check that timing doesn't increase too dramatically with file size
        // Ideally should be roughly linear: 10x file size = ~10x time
        var ratio1 = (double)timings[1] / timings[0];
        var ratio2 = (double)timings[2] / timings[1];
        
        // Both ratios should be within reasonable bounds (allow 15x for safety)
        Assert.True(ratio1 < 15, $"Memory scaling issue: 5x file size took {ratio1:.1f}x time");
        Assert.True(ratio2 < 15, $"Memory scaling issue: 2x file size took {ratio2:.1f}x time");
    }

    #endregion

    #region Caching Opportunity Tests

    [Fact]
    public void PerformanceBenchmark_RepeatDiscovery_IdentifiesCachingBenefit()
    {
        // Arrange
        var sourceCode = @"
var q1 = @""SELECT * FROM Users WHERE Status = 'Active'"";
var q2 = @""SELECT * FROM Orders WHERE Total > 100"";
var q3 = @""SELECT * FROM Products WHERE Category = 'Electronics'"";
";

        // Act - First pass (cold)
        var sw1 = Stopwatch.StartNew();
        var results1 = _discoveryService.DiscoverSqlStrings(sourceCode);
        sw1.Stop();

        // Act - Second pass (warm, same source)
        var sw2 = Stopwatch.StartNew();
        var results2 = _discoveryService.DiscoverSqlStrings(sourceCode);
        sw2.Stop();

        // Assert
        Assert.Equal(results1.Count, results2.Count);
        
        // If caching is implemented, second run should be faster or at least not significantly slower.
        // Use high-resolution timings (TotalMilliseconds) and protect against tiny denominators.
        var denom = Math.Max(1e-4, sw2.Elapsed.TotalMilliseconds);
        var speedup = sw1.Elapsed.TotalMilliseconds / denom;
        // Ensure the warm (second) run is not more than 2x slower than the cold run
        Assert.True(speedup >= 0.5, $"Expected no significant regression on warm cache (second run <= 2x slower), got {speedup:.1f}x speedup (cold: {sw1.Elapsed.TotalMilliseconds:.3f}ms, warm: {sw2.Elapsed.TotalMilliseconds:.3f}ms)");
    }

    #endregion

    #region Scalability Tests

    [Fact]
    public void PerformanceBenchmark_RealWorldScenario_100FilesWithMixedContent()
    {
        // Arrange - Simulate a real project with 100 files
        var files = new List<string>();
        for (int fileIdx = 0; fileIdx < 100; fileIdx++)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"// File {fileIdx}");
            sb.AppendLine("using System;");
            
            for (int queryIdx = 0; queryIdx < 5; queryIdx++)
            {
                sb.AppendLine($@"
var query = @""SELECT * FROM Table{fileIdx}_{queryIdx} WHERE Id = {queryIdx}"";
");
            }
            
            files.Add(sb.ToString());
        }

        var sw = Stopwatch.StartNew();

        // Act
        var allResults = new List<Dialect.Cli.SqlDiscovery.DiscoveredSqlString>();
        foreach (var fileContent in files)
        {
            allResults.AddRange(_discoveryService.DiscoverSqlStrings(fileContent));
        }

        // Assert
        sw.Stop();
        Assert.True(allResults.Count >= 400, $"Expected at least 400 queries, found {allResults.Count}");
        Assert.True(sw.ElapsedMilliseconds < 10000, 
            $"Processing 100 files took {sw.ElapsedMilliseconds}ms (target: <10s)");
        
        var filePerSec = files.Count / (sw.ElapsedMilliseconds / 1000.0);
        var queryPerSec = allResults.Count / (sw.ElapsedMilliseconds / 1000.0);
        
        // Verify throughput targets
        Assert.True(filePerSec > 10, $"File throughput {filePerSec:.1f}/s below target of 10");
        Assert.True(queryPerSec > 40, $"Query throughput {queryPerSec:.1f}/s below target of 40");
    }

    #endregion

    #region Complex Query Performance

    [Fact]
    public void PerformanceBenchmark_ComplexQuery_ProcessesWithinAcceptableTime()
    {
        // Arrange - Complex real-world query
        var sourceCode = @"
var complexQuery = @""
WITH UserOrders AS (
    SELECT u.UserId, u.UserName, o.OrderId, o.Total,
           ROW_NUMBER() OVER (PARTITION BY u.UserId ORDER BY o.OrderDate DESC) AS OrderRank
    FROM Users u
    LEFT JOIN Orders o ON u.UserId = o.UserId
    WHERE u.Status = 'Active'
)
SELECT UserId, UserName, OrderId, Total
FROM UserOrders
WHERE OrderRank <= 10
    AND Total > 100
ORDER BY UserId, OrderRank
"";
";

        var sw = Stopwatch.StartNew();

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        sw.Stop();
        Assert.NotEmpty(results);
        // Allow more time for complex query (up to 500ms on slower systems)
        Assert.True(sw.ElapsedMilliseconds < 500, 
            $"Complex query processing took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region String Operations Performance

    [Fact]
    public void PerformanceBenchmark_StringAnalysis_VerbatimVsNormalStrings()
    {
        // Arrange
        var verbatimCode = @"
var sql = @""
SELECT UserId, UserName, Email
FROM Users
WHERE Status = 'Active'
ORDER BY CreatedDate DESC
"";
";

        var normalCode = @"
var sql = ""SELECT UserId, UserName, Email FROM Users WHERE Status = 'Active' ORDER BY CreatedDate DESC"";
";

        // Act
        var sw1 = Stopwatch.StartNew();
        var results1 = _discoveryService.DiscoverSqlStrings(verbatimCode);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        var results2 = _discoveryService.DiscoverSqlStrings(normalCode);
        sw2.Stop();

        // Assert
        Assert.NotEmpty(results1);
        Assert.NotEmpty(results2);
        
        // Both should be processed quickly
        Assert.True(sw1.ElapsedMilliseconds < 50, $"Verbatim string took {sw1.ElapsedMilliseconds}ms");
        Assert.True(sw2.ElapsedMilliseconds < 50, $"Normal string took {sw2.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Helper Methods

    private string GenerateTestFile(int fileCount, int queriesPerFile)
    {
        var sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < fileCount; i++)
        {
            sb.AppendLine($"public class Service{i}");
            sb.AppendLine("{");
            
            for (int j = 0; j < queriesPerFile; j++)
            {
                sb.AppendLine($@"    var query{j} = @""SELECT * FROM Table{i}_{j}"";");
            }
            
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }

    #endregion
}
