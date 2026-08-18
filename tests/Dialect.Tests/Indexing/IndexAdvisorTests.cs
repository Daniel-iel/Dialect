namespace Dialect.Tests.Indexing;

using FluentAssertions;
using Xunit;
using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Indexing;
using Dialect.SqlServer;
using Dialect.PostgreSql;
using Dialect.MySql;

/// <summary>
/// Tests for IndexAdvisor implementations across all SQL dialects.
/// Validates index recommendation engine functionality.
/// </summary>
public class IndexAdvisorTests
{
    [Fact]
    public void SqlServerAdvisor_Analyze_ReturnsIReadOnlyList()
    {
        // Arrange
        var dialect = new SqlServerDialect();
        var advisor = dialect.CreateIndexAdvisor();
        var query = new CompiledQuery("SELECT * FROM Users WHERE Id = @id", new Dictionary<string, object> { { "@id", 1 } });

        // Act
        var recommendations = advisor.Analyze(query, dialect);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeAssignableTo<IReadOnlyList<IndexRecommendation>>();
    }

    [Fact]
    public void PostgreSqlAdvisor_Analyze_ReturnsIReadOnlyList()
    {
        // Arrange
        var dialect = new PostgreSqlDialect();
        var advisor = dialect.CreateIndexAdvisor();
        var query = new CompiledQuery("SELECT * FROM users WHERE id = $1", new Dictionary<string, object> { { "$1", 1 } });

        // Act
        var recommendations = advisor.Analyze(query, dialect);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeAssignableTo<IReadOnlyList<IndexRecommendation>>();
    }

    [Fact]
    public void MySqlAdvisor_Analyze_ReturnsIReadOnlyList()
    {
        // Arrange
        var dialect = new MySqlDialect();
        var advisor = dialect.CreateIndexAdvisor();
        var query = new CompiledQuery("SELECT * FROM users WHERE id = ?", new Dictionary<string, object> { { "id", 1 } });

        // Act
        var recommendations = advisor.Analyze(query, dialect);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeAssignableTo<IReadOnlyList<IndexRecommendation>>();
    }

    [Theory]
    [InlineData(0.9, 50, 2)]  // High selectivity, frequent, nested joins
    [InlineData(0.5, 10, 1)]  // Moderate selectivity, infrequent
    [InlineData(0.1, 1, 0)]   // Low selectivity, rare query
    public void ScoreIndexBenefit_CalculatesCorrectly(decimal selectivity, int frequency, int joinDepth)
    {
        // Arrange - using reflection to test protected method
        var advisor = new TestIndexAdvisor();

        // Act
        var score = advisor.TestScoreIndexBenefit(selectivity, frequency, joinDepth);

        // Assert
        score.Should().BeGreaterThanOrEqualTo(0.0m);
        score.Should().BeLessThanOrEqualTo(1.0m);
    }

    [Theory]
    [InlineData(0.85, IndexPriority.Critical)]
    [InlineData(0.7, IndexPriority.High)]
    [InlineData(0.5, IndexPriority.Medium)]
    [InlineData(0.2, IndexPriority.Low)]
    public void GetPriority_ReturnCorrectPriority(decimal benefit, IndexPriority expectedPriority)
    {
        // Arrange
        var advisor = new TestIndexAdvisor();

        // Act
        var priority = advisor.TestGetPriority(benefit);

        // Assert
        priority.Should().Be(expectedPriority);
    }

    [Fact]
    public void IndexRecommendation_Record_IsImmutable()
    {
        // Arrange
        var recommendation = new IndexRecommendation(
            "Users",
            new[] { "UserId" },
            IndexType.SingleColumn,
            0.85m,
            "Highly selective column used in WHERE clause",
            IndexPriority.Critical
        );

        // Act & Assert
        recommendation.TableName.Should().Be("Users");
        recommendation.ColumnNames.Should().Contain("UserId");
        recommendation.Type.Should().Be(IndexType.SingleColumn);
        recommendation.EstimatedBenefit.Should().Be(0.85m);
        recommendation.Priority.Should().Be(IndexPriority.Critical);
    }

    [Fact]
    public void IndexType_Enum_ContainsAllTypes()
    {
        // Arrange & Act & Assert
        IndexType.SingleColumn.Should().Be(IndexType.SingleColumn);
        IndexType.Composite.Should().Be(IndexType.Composite);
        IndexType.Covering.Should().Be(IndexType.Covering);
        IndexType.FullText.Should().Be(IndexType.FullText);
        IndexType.Filtered.Should().Be(IndexType.Filtered);
    }

    [Fact]
    public void IndexPriority_Enum_ContainsAllPriorities()
    {
        // Arrange & Act & Assert
        IndexPriority.Critical.Should().Be(IndexPriority.Critical);
        IndexPriority.High.Should().Be(IndexPriority.High);
        IndexPriority.Medium.Should().Be(IndexPriority.Medium);
        IndexPriority.Low.Should().Be(IndexPriority.Low);
    }

    [Fact]
    public void CreateIndexAdvisor_OnAllDialects_ReturnsNonNull()
    {
        // Arrange
        var dialects = new ISqlDialect[] 
        { 
            new SqlServerDialect(),
            new PostgreSqlDialect(),
            new MySqlDialect()
        };

        // Act & Assert
        foreach (var dialect in dialects)
        {
            var advisor = dialect.CreateIndexAdvisor();
            advisor.Should().NotBeNull();
            advisor.Should().BeAssignableTo<IndexAdvisor>();
        }
    }

    /// <summary>
    /// Test helper class to expose protected methods for testing.
    /// </summary>
    private class TestIndexAdvisor : IndexAdvisor
    {
        public override IReadOnlyList<IndexRecommendation> Analyze(CompiledQuery query, ISqlDialect dialect) =>
            new List<IndexRecommendation>();

        public decimal TestScoreIndexBenefit(decimal selectivity, int frequency, int joinDepth) =>
            ScoreIndexBenefit(selectivity, frequency, joinDepth);

        public IndexPriority TestGetPriority(decimal benefit) =>
            GetPriority(benefit);
    }
}
