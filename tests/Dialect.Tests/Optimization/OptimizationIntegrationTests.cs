using Dialect.Core.Optimization;
using Dialect.Core.Performance;
using Dialect.SqlServer.Optimization;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class OptimizationIntegrationTests
{
    [Fact]
    public void FullOptimizationWorkflow_GeneratesAndPrioritizesRecommendations()
    {
        // Arrange
        var optimizer = new SqlServerOptimizer();
        var prioritizer = new RecommendationPrioritizer();
        
        // Create a slow query execution plan
        var node = new ExecutionPlanNode(
            OperationType: "TableScan",
            RowsProduced: 50000,
            RowsExamined: 100000,
            Cost: 80.0m,
            ObjectName: "Orders",
            Predicate: "Status = 'Pending'",
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 80.0m,
            TotalRowsProduced: 50000,
            ExecutionTimeMs: 2000,
            QueryText: "SELECT * FROM Orders WHERE Status = 'Pending'",
            Metadata: new Dictionary<string, object>()
        );
        
        var metrics = new PerformanceMetrics(
            TotalCost: 80.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 100000,
            TotalRowsProduced: 50000,
            Selectivity: 0.5,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 2000,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = optimizer.GenerateRecommendations(
            "SELECT * FROM Orders WHERE Status = 'Pending'",
            metrics,
            plan
        );
        
        var prioritized = prioritizer.Prioritize(recommendations, maxRecommendations: 5);
        
        // Assert
        recommendations.Should().NotBeEmpty();
        prioritized.Should().HaveCountLessThanOrEqualTo(5);
        prioritized.All(r => r.IsValid).Should().BeTrue();
        prioritized.Should().BeInDescendingOrder(r => r.Priority);
    }
    
    [Fact]
    public void PrioritizeByCategory_SeparatesRecommendationsByType()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer();
        
        var indexRec = new OptimizationRecommendation(
            RecommendationId: "INDEX_1",
            Category: "Index",
            Title: "Add index",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 40,
            ImplementationCost: 15,
            Priority: 4,
            RoiScore: 2.67m,
            AffectedObjects: new List<string> { "Orders" },
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 10,
            References: new List<string>()
        );
        
        var queryRec = new OptimizationRecommendation(
            RecommendationId: "QUERY_1",
            Category: "QueryRewrite",
            Title: "Rewrite query",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 25,
            ImplementationCost: 30,
            Priority: 3,
            RoiScore: 0.83m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Medium",
            EstimatedImplementationTimeMinutes: 30,
            References: new List<string>()
        );
        
        var joinRec = new OptimizationRecommendation(
            RecommendationId: "JOIN_1",
            Category: "Join",
            Title: "Optimize join",
            Description: "Test",
            SqlStatement: null,
            ImprovementPercentage: 35,
            ImplementationCost: 20,
            Priority: 3,
            RoiScore: 1.75m,
            AffectedObjects: new List<string>(),
            DialectSpecificNotes: new Dictionary<string, string>(),
            RiskLevel: "Low",
            EstimatedImplementationTimeMinutes: 15,
            References: new List<string>()
        );
        
        // Act
        var grouped = prioritizer.PrioritizeByCategory(
            new[] { indexRec, queryRec, joinRec },
            topPerCategory: 1
        );
        
        // Assert
        grouped.Should().HaveCount(3);
        grouped["Index"].Should().HaveCount(1);
        grouped["QueryRewrite"].Should().HaveCount(1);
        grouped["Join"].Should().HaveCount(1);
    }
    
    [Fact]
    public void FilterByRiskAndRoi_RemovesHighRiskOrLowValueRecommendations()
    {
        // Arrange
        var prioritizer = new RecommendationPrioritizer();
        
        var recommendations = new[]
        {
            new OptimizationRecommendation("SAFE_HIGH", "Index", "Safe High ROI", "T", null, 50, 10, 4, 5.0m, new List<string>(), new Dictionary<string, string>(), "Low", 10, new List<string>()),
            new OptimizationRecommendation("MEDIUM_HIGH", "Index", "Medium Risk High ROI", "T", null, 50, 25, 4, 2.0m, new List<string>(), new Dictionary<string, string>(), "Medium", 20, new List<string>()),
            new OptimizationRecommendation("HIGH_RISK", "Index", "High Risk", "T", null, 50, 50, 3, 1.0m, new List<string>(), new Dictionary<string, string>(), "High", 60, new List<string>()),
            new OptimizationRecommendation("LOW_ROI", "Index", "Low ROI", "T", null, 10, 80, 1, 0.125m, new List<string>(), new Dictionary<string, string>(), "Low", 120, new List<string>()),
        };
        
        // Act
        var filtered = prioritizer.FilterByRiskAndRoi(recommendations, maxRiskLevel: "Medium", minRoiScore: 0.5m);
        
        // Assert
        filtered.Should().Contain(r => r.RecommendationId == "SAFE_HIGH");
        filtered.Should().Contain(r => r.RecommendationId == "MEDIUM_HIGH");
        filtered.Should().NotContain(r => r.RecommendationId == "HIGH_RISK");
        filtered.Should().NotContain(r => r.RecommendationId == "LOW_ROI");
    }
    
    [Fact]
    public void OptimizationEngine_GeneratesRecommendationsWithCorrectCategories()
    {
        // Arrange
        var optimizer = new SqlServerOptimizer();
        
        var node = new ExecutionPlanNode(
            OperationType: "NestedLoopJoin",
            RowsProduced: 10000,
            RowsExamined: 10000,
            Cost: 20.0m,
            ObjectName: "Orders",
            Predicate: null,
            Children: new[]
            {
                new ExecutionPlanNode(
                    OperationType: "TableScan",
                    RowsProduced: 5000,
                    RowsExamined: 5000,
                    Cost: 10.0m,
                    ObjectName: "Customers",
                    Predicate: null,
                    Children: Array.Empty<ExecutionPlanNode>(),
                    Properties: new Dictionary<string, object>()
                )
            },
            Properties: new Dictionary<string, object>()
        );
        
        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 20.0m,
            TotalRowsProduced: 10000,
            ExecutionTimeMs: 500,
            QueryText: "SELECT * FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
            Metadata: new Dictionary<string, object>()
        );
        
        var metrics = new PerformanceMetrics(
            TotalCost: 20.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 1,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 10000,
            TotalRowsProduced: 10000,
            Selectivity: 1.0,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 500,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = optimizer.GenerateRecommendations(
            "SELECT * FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
            metrics,
            plan
        );
        
        // Assert
        recommendations.Should().Contain(r => r.Category == "Index");
        recommendations.Should().Contain(r => r.Category == "QueryRewrite");
    }
    
    [Fact]
    public void RecommendationWithSqlStatement_ContainsImplementationHint()
    {
        // Arrange
        var optimizer = new SqlServerOptimizer();
        
        var node = new ExecutionPlanNode(
            OperationType: "TableScan",
            RowsProduced: 10000,
            RowsExamined: 10000,
            Cost: 10.0m,
            ObjectName: "Orders",
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 10.0m,
            TotalRowsProduced: 10000,
            ExecutionTimeMs: 100,
            QueryText: "SELECT * FROM Orders",
            Metadata: new Dictionary<string, object>()
        );
        
        var metrics = new PerformanceMetrics(
            TotalCost: 10.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 10000,
            TotalRowsProduced: 10000,
            Selectivity: 1.0,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 100,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        var indexRec = recommendations.FirstOrDefault(r => r.Category == "Index");
        
        // Assert
        indexRec.Should().NotBeNull();
        indexRec!.SqlStatement.Should().NotBeNullOrWhiteSpace();
        indexRec.SqlStatement.Should().Contain("CREATE INDEX");
    }
}
