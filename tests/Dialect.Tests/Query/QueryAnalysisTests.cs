namespace Dialect.Tests.Query;

using FluentAssertions;
using Xunit;
using Dialect.Core.Query;

/// <summary>
/// Tests for PredicateAnalyzer, JoinAnalyzer, and AggregationAnalyzer.
/// Validates detailed query clause analysis.
/// </summary>
public class QueryAnalysisTests
{
    // ========== PredicateAnalyzer Tests ==========

    [Theory]
    [InlineData("Id = 1", "EQUALITY")]
    [InlineData("Status <> 'Active'", "INEQUALITY")]
    [InlineData("Age > 18", "COMPARISON")]
    [InlineData("CreatedDate >= '2024-01-01'", "RANGE")]
    [InlineData("Name BETWEEN 'A' AND 'Z'", "BETWEEN")]
    [InlineData("Status IN ('Active', 'Pending')", "IN_LIST")]
    [InlineData("Name LIKE '%John%'", "LIKE")]
    [InlineData("DeletedAt IS NULL", "IS_NULL")]
    public void PredicateAnalyzer_DetermineOperatorType_IdentifiesCorrectly(string predicate, string expectedType)
    {
        // Arrange
        var analyzer = new PredicateAnalyzer();

        // Act
        var analysis = analyzer.Analyze(predicate);

        // Assert
        analysis.OperatorType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("Id = 1", true)]      // Equality is indexable
    [InlineData("Age > 18", true)]     // Comparison is indexable
    [InlineData("Status IN ('A','B')", true)]  // IN is indexable
    [InlineData("Name LIKE '%test%'", false)]  // Leading wildcard not indexable
    [InlineData("Id IS NULL", false)]  // IS NULL not indexable
    public void PredicateAnalyzer_IsIndexable_CorrectlyDetermines(string predicate, bool expectedIndexable)
    {
        // Arrange
        var analyzer = new PredicateAnalyzer();

        // Act
        var analysis = analyzer.Analyze(predicate);

        // Assert
        analysis.IsIndexable.Should().Be(expectedIndexable);
    }

    [Theory]
    [InlineData("Id = 1", 0.05f, 0.15f)]  // Equality: highly selective
    [InlineData("Age > 18", 0.35f, 0.45f)]  // Comparison: moderate
    [InlineData("Status IN ('A', 'B')", 0.2f, 0.3f)]  // IN list: lower selectivity
    public void PredicateAnalyzer_SelectivityEstimate_RangeCorrect(string predicate, float minExpected, float maxExpected)
    {
        // Arrange
        var analyzer = new PredicateAnalyzer();

        // Act
        var analysis = analyzer.Analyze(predicate);

        // Assert
        analysis.Selectivity.Should().BeGreaterThanOrEqualTo((decimal)minExpected);
        analysis.Selectivity.Should().BeLessThanOrEqualTo((decimal)maxExpected);
    }

    [Fact]
    public void PredicateAnalysis_IndexPriority_HighForSelectiveIndexablePredicates()
    {
        // Arrange
        var analyzer = new PredicateAnalyzer();

        // Act
        var highSelectivity = analyzer.Analyze("Id = 1");      // Equality
        var lowSelectivity = analyzer.Analyze("LIKE '%test%'"); // LIKE

        // Assert
        highSelectivity.IndexPriority.Should().BeGreaterThan(lowSelectivity.IndexPriority);
    }

    // ========== JoinAnalyzer Tests ==========

    [Theory]
    [InlineData("INNER JOIN Orders ON Users.Id = Orders.UserId", "INNER_JOIN")]
    [InlineData("LEFT JOIN Orders ON Users.Id = Orders.UserId", "LEFT_JOIN")]
    [InlineData("RIGHT JOIN Orders ON Users.Id = Orders.UserId", "RIGHT_JOIN")]
    [InlineData("FULL JOIN Orders ON Users.Id = Orders.UserId", "FULL_JOIN")]
    [InlineData("CROSS JOIN Orders", "CROSS_JOIN")]
    public void JoinAnalyzer_DetermineJoinType_IdentifiesCorrectly(string clause, string expectedType)
    {
        // Arrange
        var analyzer = new JoinAnalyzer();

        // Act
        var analysis = analyzer.Analyze(clause);

        // Assert
        analysis.JoinType.Should().Be(expectedType);
    }

    [Fact]
    public void JoinAnalyzer_AnalyzeInnerJoin_MarkAsOptimal()
    {
        // Arrange
        var analyzer = new JoinAnalyzer();
        const string clause = "INNER JOIN Orders ON Users.Id = Orders.UserId";

        // Act
        var analysis = analyzer.Analyze(clause);

        // Assert
        analysis.IsOptimal.Should().BeTrue();
        analysis.JoinType.Should().Be("INNER_JOIN");
    }

    [Fact]
    public void JoinAnalyzer_AnalyzeFullJoin_MarkAsSuboptimal()
    {
        // Arrange
        var analyzer = new JoinAnalyzer();
        const string clause = "FULL JOIN Orders ON Users.Id = Orders.UserId";

        // Act
        var analysis = analyzer.Analyze(clause);

        // Assert
        analysis.IsOptimal.Should().BeFalse();
        analysis.Recommendation.Should().Contain("FULL JOIN");
    }

    [Fact]
    public void JoinAnalyzer_AnalyzeCrossJoin_ProvidesWarning()
    {
        // Arrange
        var analyzer = new JoinAnalyzer();
        const string clause = "CROSS JOIN Orders";

        // Act
        var analysis = analyzer.Analyze(clause);

        // Assert
        analysis.Recommendation.Should().Contain("CROSS JOIN");
        analysis.Recommendation.Should().Contain("expensive");
    }

    // ========== AggregationAnalyzer Tests ==========

    [Theory]
    [InlineData(new[] { "COUNT(*)" }, "COUNT")]
    [InlineData(new[] { "SUM(Amount)" }, "SUM")]
    [InlineData(new[] { "AVG(Price)" }, "AVG")]
    [InlineData(new[] { "MIN(Price)" }, "MIN")]
    [InlineData(new[] { "MAX(Price)" }, "MAX")]
    public void AggregationAnalyzer_ParseAggregateFunction_IdentifiesType(string[] functions, string expectedType)
    {
        // Arrange
        var analyzer = new AggregationAnalyzer();

        // Act
        var analysis = analyzer.Analyze("Department", functions);

        // Assert
        analysis.AggregateFunctions.Should().NotBeEmpty();
        analysis.AggregateFunctions[0].Type.Should().Be(expectedType);
    }

    [Fact]
    public void AggregationAnalyzer_GroupByDepartment_ExtractsColumns()
    {
        // Arrange
        var analyzer = new AggregationAnalyzer();

        // Act
        var analysis = analyzer.Analyze("Department, Region", new[] { "COUNT(*)" });

        // Assert
        analysis.GroupColumns.Should().Contain("Department");
        analysis.GroupColumns.Should().Contain("Region");
    }

    [Fact]
    public void AggregationAnalyzer_SimpleGroupBy_IsOptimal()
    {
        // Arrange
        var analyzer = new AggregationAnalyzer();

        // Act
        var analysis = analyzer.Analyze("Department", new[] { "COUNT(*)", "SUM(Sales)" });

        // Assert
        analysis.IsOptimal.Should().BeTrue();
        analysis.CanUseIndex.Should().BeTrue();
    }

    [Fact]
    public void AggregationAnalyzer_ManyGroupByColumns_IsNotOptimal()
    {
        // Arrange
        var analyzer = new AggregationAnalyzer();
        var columns = string.Join(", ", Enumerable.Range(1, 5).Select(i => $"Col{i}"));

        // Act
        var analysis = analyzer.Analyze(columns, new[] { "COUNT(*)" });

        // Assert
        analysis.IsOptimal.Should().BeFalse();
    }

    [Fact]
    public void AggregateFunction_Record_ContainsAllProperties()
    {
        // Arrange & Act
        var func = new AggregateFunction(
            OriginalFunction: "COUNT(DISTINCT user_id)",
            Type: "COUNT",
            Column: "user_id"
        );

        // Assert
        func.OriginalFunction.Should().Be("COUNT(DISTINCT user_id)");
        func.Type.Should().Be("COUNT");
        func.Column.Should().Be("user_id");
    }

    // ========== Integration Tests ==========

    [Fact]
    public void PredicateAnalyzer_ComplexAndOrCondition_HandlesMultiplePredicates()
    {
        // Arrange
        var analyzer = new PredicateAnalyzer();

        // Act
        var predicate1 = analyzer.Analyze("Status = 'Active'");
        var predicate2 = analyzer.Analyze("CreatedDate > '2024-01-01'");

        // Assert
        predicate1.IsIndexable.Should().BeTrue();
        predicate2.IsIndexable.Should().BeTrue();
        predicate1.Selectivity.Should().BeLessThan(predicate2.Selectivity);  // Equality more selective
    }

    [Fact]
    public void JoinAndPredicateAnalysis_Together_ProvidesOptimizationTips()
    {
        // Arrange
        var joinAnalyzer = new JoinAnalyzer();
        var predicateAnalyzer = new PredicateAnalyzer();

        const string joinClause = "INNER JOIN Orders ON Users.Id = Orders.UserId";
        const string predicate = "Orders.Status = 'Complete'";

        // Act
        var joinAnalysis = joinAnalyzer.Analyze(joinClause);
        var predicateAnalysis = predicateAnalyzer.Analyze(predicate);

        // Assert
        joinAnalysis.IsOptimal.Should().BeTrue();
        predicateAnalysis.IsIndexable.Should().BeTrue();
    }

    [Fact]
    public void AllAnalyzers_WorkTogether_ProvidingComprehensiveAnalysis()
    {
        // This demonstrates that all three analyzers can be used together
        // to get a complete picture of query optimization opportunities

        // Arrange
        var predicateAnalyzer = new PredicateAnalyzer();
        var joinAnalyzer = new JoinAnalyzer();
        var aggAnalyzer = new AggregationAnalyzer();

        // Act
        var predAnalysis = predicateAnalyzer.Analyze("User.Status = 'Active'");
        var joinAnalysis = joinAnalyzer.Analyze("INNER JOIN Orders ON User.Id = Orders.UserId");
        var aggAnalysis = aggAnalyzer.Analyze("Department, Region", new[] { "COUNT(*)", "SUM(Amount)" });

        // Assert
        predAnalysis.Should().NotBeNull();
        joinAnalysis.Should().NotBeNull();
        aggAnalysis.Should().NotBeNull();

        predAnalysis.IsIndexable.Should().BeTrue();
        joinAnalysis.IsOptimal.Should().BeTrue();
        aggAnalysis.IsOptimal.Should().BeTrue();
    }
}
