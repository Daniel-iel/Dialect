using Dialect.Core.Optimization;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class RecommendationPrioritizerTests
{
    private readonly RecommendationPrioritizer _prioritizer = new();
    
    private OptimizationRecommendation CreateRecommendation(
        string id,
        decimal roiScore,
        int priority,
        decimal improvement,
        int implementationCost)
    {
        return new OptimizationRecommendation(
            RecommendationId: id,
            Category: "Index",
            Title: $"Recommendation {id}",
            Description: "Test recommendation",
            SqlStatement: null,
            ImprovementPercentage: improvement,
            ImplementationCost: implementationCost,
            Priority: priority,
            RoiScore: roiScore,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 10,
            References: new List<string>()
        );
    }
    
    [Fact]
    public void Prioritize_SortsByRoiAndPriorityByDefault()
    {
        // Arrange
        var recommendations = new[]
        {
            CreateRecommendation("REC1", roiScore: 1.0m, priority: 2, improvement: 30, implementationCost: 20),
            CreateRecommendation("REC2", roiScore: 3.0m, priority: 4, improvement: 60, implementationCost: 20),
            CreateRecommendation("REC3", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25),
        };
        
        // Act
        var result = _prioritizer.Prioritize(recommendations);
        
        // Assert
        result.Should().HaveCount(3);
        result[0].RecommendationId.Should().Be("REC2");  // Highest ROI and priority
    }
    
    [Fact]
    public void Prioritize_RespectsMaxRecommendations()
    {
        // Arrange
        var recommendations = Enumerable.Range(1, 20)
            .Select(i => CreateRecommendation($"REC{i}", roiScore: i, priority: i, improvement: i * 5, implementationCost: 10))
            .ToList();
        
        // Act
        var result = _prioritizer.Prioritize(recommendations, maxRecommendations: 5);
        
        // Assert
        result.Should().HaveCount(5);
    }
    
    [Fact]
    public void Prioritize_WithRoiOnlyStrategy()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer(PrioritizationStrategy.RoiOnly);
        var recommendations = new[]
        {
            CreateRecommendation("REC1", roiScore: 0.5m, priority: 5, improvement: 80, implementationCost: 90),
            CreateRecommendation("REC2", roiScore: 5.0m, priority: 1, improvement: 20, implementationCost: 5),
            CreateRecommendation("REC3", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25),
        };
        
        // Act
        var result = prioritizer.Prioritize(recommendations);
        
        // Assert
        result[0].RecommendationId.Should().Be("REC2");  // Highest ROI
        result[1].RecommendationId.Should().Be("REC3");
        result[2].RecommendationId.Should().Be("REC1");
    }
    
    [Fact]
    public void Prioritize_WithPriorityOnlyStrategy()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer(PrioritizationStrategy.PriorityOnly);
        var recommendations = new[]
        {
            CreateRecommendation("REC1", roiScore: 5.0m, priority: 1, improvement: 80, implementationCost: 5),
            CreateRecommendation("REC2", roiScore: 0.5m, priority: 5, improvement: 20, implementationCost: 90),
            CreateRecommendation("REC3", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25),
        };
        
        // Act
        var result = prioritizer.Prioritize(recommendations);
        
        // Assert
        result[0].RecommendationId.Should().Be("REC2");  // Highest priority (5)
        result[1].RecommendationId.Should().Be("REC3");  // Priority 3
        result[2].RecommendationId.Should().Be("REC1");  // Priority 1
    }
    
    [Fact]
    public void Prioritize_WithImpactFirstStrategy()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer(PrioritizationStrategy.ImpactFirst);
        var recommendations = new[]
        {
            CreateRecommendation("REC1", roiScore: 0.5m, priority: 5, improvement: 80, implementationCost: 90),
            CreateRecommendation("REC2", roiScore: 5.0m, priority: 1, improvement: 20, implementationCost: 5),
            CreateRecommendation("REC3", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25),
        };
        
        // Act
        var result = prioritizer.Prioritize(recommendations);
        
        // Assert
        result[0].RecommendationId.Should().Be("REC1");  // 80% improvement
        result[1].RecommendationId.Should().Be("REC3");  // 50% improvement
        result[2].RecommendationId.Should().Be("REC2");  // 20% improvement
    }
    
    [Fact]
    public void Prioritize_WithLowEffortFirstStrategy()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer(PrioritizationStrategy.LowEffortFirst);
        var recommendations = new[]
        {
            CreateRecommendation("REC1", roiScore: 0.5m, priority: 5, improvement: 80, implementationCost: 90),
            CreateRecommendation("REC2", roiScore: 5.0m, priority: 1, improvement: 20, implementationCost: 5),
            CreateRecommendation("REC3", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25),
        };
        
        // Act
        var result = prioritizer.Prioritize(recommendations);
        
        // Assert
        result[0].RecommendationId.Should().Be("REC2");  // Cost 5
        result[1].RecommendationId.Should().Be("REC3");  // Cost 25
        result[2].RecommendationId.Should().Be("REC1");  // Cost 90
    }
    
    [Fact]
    public void PrioritizeByCategory_GroupsRecommendations()
    {
        // Arrange
        var recommendations = new[]
        {
            new OptimizationRecommendation("INDEX1", "Index", "Index Rec 1", "Test", null, 30, 20, 3, 1.5m, new List<string>(), new Dictionary<string, string>(), "Low", 10, new List<string>()),
            new OptimizationRecommendation("INDEX2", "Index", "Index Rec 2", "Test", null, 40, 20, 4, 2.0m, new List<string>(), new Dictionary<string, string>(), "Low", 10, new List<string>()),
            new OptimizationRecommendation("QUERY1", "QueryRewrite", "Query Rec 1", "Test", null, 50, 30, 3, 1.67m, new List<string>(), new Dictionary<string, string>(), "Low", 20, new List<string>()),
            new OptimizationRecommendation("JOIN1", "Join", "Join Rec 1", "Test", null, 25, 25, 2, 1.0m, new List<string>(), new Dictionary<string, string>(), "Low", 15, new List<string>()),
        };
        
        // Act
        var result = _prioritizer.PrioritizeByCategory(recommendations, topPerCategory: 2);
        
        // Assert
        result.Should().HaveCount(3);
        result["Index"].Should().HaveCount(2);
        result["QueryRewrite"].Should().HaveCount(1);
        result["Join"].Should().HaveCount(1);
    }
    
    [Fact]
    public void FilterByRiskAndRoi_FiltersAppropriately()
    {
        // Arrange
        var recommendations = new[]
        {
            CreateRecommendation("SAFE_HIGH_ROI", roiScore: 3.0m, priority: 4, improvement: 60, implementationCost: 20),  // Low risk, high ROI
            CreateRecommendation("SAFE_LOW_ROI", roiScore: 0.3m, priority: 2, improvement: 10, implementationCost: 30),   // Low risk, low ROI
            CreateRecommendation("RISKY_HIGH_ROI", roiScore: 2.0m, priority: 3, improvement: 50, implementationCost: 25), // Medium risk, high ROI
            CreateRecommendation("VERY_RISKY_LOW_ROI", roiScore: 0.1m, priority: 1, improvement: 5, implementationCost: 95), // High risk, low ROI
        };
        
        // Act
        var result = _prioritizer.FilterByRiskAndRoi(recommendations, maxRiskLevel: "Medium", minRoiScore: 0.5m);
        
        // Assert
        result.Should().Contain(r => r.RecommendationId == "SAFE_HIGH_ROI");
        result.Should().Contain(r => r.RecommendationId == "RISKY_HIGH_ROI");
        result.Should().NotContain(r => r.RecommendationId == "SAFE_LOW_ROI");
        result.Should().NotContain(r => r.RecommendationId == "VERY_RISKY_LOW_ROI");
    }
    
    [Fact]
    public void FilterByRiskAndRoi_WithLowRiskOnly()
    {
        // Arrange
        var recommendations = new[]
        {
            CreateRecommendation("LOW_RISK", roiScore: 2.0m, priority: 4, improvement: 50, implementationCost: 20),
            CreateRecommendation("MEDIUM_RISK", roiScore: 3.0m, priority: 4, improvement: 60, implementationCost: 20),
            CreateRecommendation("HIGH_RISK", roiScore: 2.5m, priority: 4, improvement: 55, implementationCost: 20),
        };
        
        // Act
        var result = _prioritizer.FilterByRiskAndRoi(recommendations, maxRiskLevel: "Low", minRoiScore: 0.5m);
        
        // Assert
        result.Should().HaveCount(1);
        result[0].RecommendationId.Should().Be("LOW_RISK");
    }
}
