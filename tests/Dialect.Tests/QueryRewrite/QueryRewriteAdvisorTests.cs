using Dialect.Core.QueryRewrite;
using Dialect.SqlServer.QueryRewrite;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.QueryRewrite;

public class QueryRewriteAdvisorTests
{
    private readonly SqlServerRewriteAdvisor _advisor = new();

    [Fact]
    public void AnalyzeQuery_ReturnsQueryRewrites()
    {
        // Arrange
        const string query = "SELECT * FROM (SELECT id FROM Orders) sub JOIN (SELECT id FROM Customers) c ON sub.id = c.id";

        // Act
        var rewrites = _advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryRewrite_IsValid()
    {
        // Arrange
        var rewrite = new global::Dialect.Core.QueryRewrite.QueryRewrite(
            RewriteId: "RW_001",
            Category: "CTE",
            Title: "Use CTE",
            Description: "Test rewrite",
            OriginalPattern: "SELECT * FROM (SELECT ...)",
            SuggestedPattern: "WITH cte AS (...) SELECT *",
            ImprovementPercentage: 15,
            ImplementationComplexity: 2,
            Priority: 4,
            RiskLevel: "Low",
            RoiScore: 7.5m,
            AffectedComponents: new List<string> { "SELECT" },
            ApplicabilityConditions: new List<string>(),
            Tradeoffs: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>()
        );

        // Assert
        rewrite.IsValid.Should().BeTrue();
    }

    [Fact]
    public void QueryRewrite_InvalidWithoutCategory()
    {
        // Arrange
        var rewrite = new global::Dialect.Core.QueryRewrite.QueryRewrite(
            RewriteId: "RW_001",
            Category: "",  // Invalid
            Title: "Use CTE",
            Description: "Test rewrite",
            OriginalPattern: "SELECT * FROM (SELECT ...)",
            SuggestedPattern: "WITH cte AS (...) SELECT *",
            ImprovementPercentage: 15,
            ImplementationComplexity: 2,
            Priority: 4,
            RiskLevel: "Low",
            RoiScore: 7.5m,
            AffectedComponents: new List<string>(),
            ApplicabilityConditions: new List<string>(),
            Tradeoffs: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>()
        );

        // Assert
        rewrite.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeQuery_DetectsCteOpportunities()
    {
        // Arrange
        const string query = "SELECT * FROM (SELECT id FROM orders) o1 JOIN (SELECT id FROM customers) c1 ON o1.id = c1.id";

        // Act
        var rewrites = _advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.Category == "CTE");
    }

    [Fact]
    public void AnalyzeQuery_DetectsWindowFunctionOpportunities()
    {
        // Arrange
        const string query = "SELECT id, COUNT(*) FROM orders GROUP BY category ORDER BY id";

        // Act
        var rewrites = _advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.Category == "WindowFunction");
    }

    [Fact]
    public void AnalyzeQuery_DetectsJoinOrderOptimization()
    {
        // Arrange
        const string query = "SELECT * FROM a JOIN b ON a.id = b.id JOIN c ON b.id = c.id JOIN d ON c.id = d.id WHERE d.status = 'active'";

        // Act
        var rewrites = _advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.Category == "JoinOrder");
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    public void QueryRewrite_AcceptsValidRiskLevels(string riskLevel)
    {
        // Arrange
        var rewrite = new global::Dialect.Core.QueryRewrite.QueryRewrite(
            RewriteId: "RW_001",
            Category: "CTE",
            Title: "Test",
            Description: "Test",
            OriginalPattern: "original",
            SuggestedPattern: "suggested",
            ImprovementPercentage: 15,
            ImplementationComplexity: 2,
            Priority: 3,
            RiskLevel: riskLevel,
            RoiScore: 7.5m,
            AffectedComponents: new List<string>(),
            ApplicabilityConditions: new List<string>(),
            Tradeoffs: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>()
        );

        // Assert
        rewrite.RiskLevel.Should().Be(riskLevel);
        rewrite.IsValid.Should().BeTrue();
    }
}
