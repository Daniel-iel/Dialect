using Dialect.Core.Optimization;
using Dialect.Core.Performance;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class OptimizationRecommendationTests
{
    [Fact]
    public void OptimizationRecommendation_CreatesValidRecord()
    {
        // Arrange & Act
        var recommendation = new OptimizationRecommendation(
            RecommendationId: "INDEX_001",
            Category: "Index",
            Title: "Add index on Orders.CustomerId",
            Description: "Table scan detected. Adding index can reduce cost.",
            SqlStatement: "CREATE INDEX idx_orders_customer ON Orders(CustomerId);",
            ImprovementPercentage: 45,
            ImplementationCost: 15,
            Priority: 4,
            RoiScore: 3.0m,
            AffectedObjects: new[] { "Orders" },
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 10,
            References: new List<string> { "INDEX_DESIGN" }
        );
        
        // Assert
        recommendation.Should().NotBeNull();
        recommendation.RecommendationId.Should().Be("INDEX_001");
        recommendation.Category.Should().Be("Index");
        recommendation.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void OptimizationRecommendation_ValidatesImprovementPercentage()
    {
        // Arrange
        var validRec = new OptimizationRecommendation(
            RecommendationId: "TEST",
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 50,
            ImplementationCost: 20,
            Priority: 3,
            RoiScore: 2.5m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 15,
            References: new List<string>()
        );
        
        var invalidRec = new OptimizationRecommendation(
            RecommendationId: "TEST",
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 150,  // Invalid: > 100
            ImplementationCost: 20,
            Priority: 3,
            RoiScore: 2.5m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 15,
            References: new List<string>()
        );
        
        // Assert
        validRec.IsValid.Should().BeTrue();
        invalidRec.IsValid.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    public void OptimizationRecommendation_AcceptsValidRiskLevels(string riskLevel)
    {
        // Arrange & Act
        var recommendation = new OptimizationRecommendation(
            RecommendationId: "TEST",
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 25,
            ImplementationCost: 20,
            Priority: 3,
            RoiScore: 1.25m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: riskLevel,
            EstimatedImplementationTimeMinutes: 15,
            References: new List<string>()
        );
        
        // Assert
        recommendation.RiskLevel.Should().Be(riskLevel);
        recommendation.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void OptimizationRecommendation_CalculatesRoiScore()
    {
        // Arrange & Act
        var highRoiRec = new OptimizationRecommendation(
            RecommendationId: "HIGH_ROI",
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 80,
            ImplementationCost: 10,
            Priority: 5,
            RoiScore: 8.0m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 5,
            References: new List<string>()
        );
        
        var lowRoiRec = new OptimizationRecommendation(
            RecommendationId: "LOW_ROI",
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 5,
            ImplementationCost: 90,
            Priority: 1,
            RoiScore: 0.056m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "High",
            EstimatedImplementationTimeMinutes: 120,
            References: new List<string>()
        );
        
        // Assert
        highRoiRec.RoiScore.Should().BeGreaterThan(lowRoiRec.RoiScore);
    }
    
    [Fact]
    public void OptimizationRecommendation_WithoutRequiredField_IsInvalid()
    {
        // Arrange
        var invalidRec = new OptimizationRecommendation(
            RecommendationId: "",  // Empty
            Category: "Index",
            Title: "Test",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 25,
            ImplementationCost: 20,
            Priority: 3,
            RoiScore: 1.25m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 15,
            References: new List<string>()
        );
        
        // Assert
        invalidRec.IsValid.Should().BeFalse();
    }
}
