namespace Dialect.Tests.Performance;

using FluentAssertions;
using Xunit;
using Dialect.Core.Performance;
using Dialect.SqlServer.Performance;
using ExecutionPlanNode = Dialect.Core.Performance.ExecutionPlanNode;

public class ExecutionPlanTests
{
    private readonly SqlServerPlanAnalyzer _analyzer = new();
    
    [Fact]
    public void QueryExecutionPlan_CreatesWithRootNode()
    {
        // Arrange
        var rootNode = new ExecutionPlanNode(
            OperationType: "TableScan",
            RowsProduced: 1000,
            RowsExamined: 1000,
            Cost: 5.5m,
            ObjectName: "Users",
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        // Act
        var plan = new QueryExecutionPlan(
            RootNode: rootNode,
            TotalCost: 5.5m,
            TotalRowsProduced: 1000,
            ExecutionTimeMs: 125.5,
            QueryText: "SELECT * FROM Users",
            Metadata: new Dictionary<string, object>()
        );
        
        // Assert
        plan.RootNode.OperationType.Should().Be("TableScan");
        plan.TotalCost.Should().Be(5.5m);
        plan.TotalRowsProduced.Should().Be(1000);
    }
    
    [Fact]
    public void ExecutionPlanNode_HandlesNestedChildren()
    {
        // Arrange
        var childNode = new ExecutionPlanNode(
            OperationType: "IndexSeek",
            RowsProduced: 100,
            RowsExamined: 100,
            Cost: 2.0m,
            ObjectName: "IX_UserId",
            Predicate: "UserId = 5",
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var rootNode = new ExecutionPlanNode(
            OperationType: "NestedLoopJoin",
            RowsProduced: 500,
            RowsExamined: 500,
            Cost: 5.5m,
            ObjectName: null,
            Predicate: null,
            Children: new[] { childNode },
            Properties: new Dictionary<string, object>()
        );
        
        // Act & Assert
        rootNode.Children.Should().HaveCount(1);
        rootNode.Children.First().OperationType.Should().Be("IndexSeek");
    }
    
    [Fact]
    public void PerformanceMetrics_CalculatesSelectivity()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalCost: 10.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 1,
            TotalRowsExamined: 100000,
            TotalRowsProduced: 100,
            Selectivity: 0.001,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 50,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act & Assert
        metrics.Selectivity.Should().Be(0.001);
        metrics.TotalRowsExamined.Should().Be(100000);
        metrics.TotalRowsProduced.Should().Be(100);
    }
    
    [Fact]
    public void PerformanceMetrics_FlagsTableScans()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalCost: 15.0m,
            TableScanCount: 2,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 500000,
            TotalRowsProduced: 500000,
            Selectivity: 1.0,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 200,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act & Assert
        metrics.HasTableScan.Should().BeTrue();
        metrics.TableScanCount.Should().Be(2);
    }
    
    [Fact]
    public void PerformanceMetrics_FlagsIneffectiveNestedLoops()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalCost: 50.0m,
            TableScanCount: 0,
            IndexSeekCount: 2,
            IndexScanCount: 0,
            NestedLoopJoinCount: 3,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 50000,
            TotalRowsProduced: 1000,
            Selectivity: 0.02,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: true,
            ExecutionTimeMs: 300,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act & Assert
        metrics.HasIneffectiveNestedLoop.Should().BeTrue();
        metrics.NestedLoopJoinCount.Should().Be(3);
    }
    
    [Fact]
    public void PerformanceMetrics_IdentifiesSortOperations()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalCost: 25.0m,
            TableScanCount: 0,
            IndexSeekCount: 1,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 1,
            FilterOperationCount: 0,
            TotalRowsExamined: 20000,
            TotalRowsProduced: 15000,
            Selectivity: 0.75,
            HasTableScan: false,
            HasSort: true,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 150,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act & Assert
        metrics.HasSort.Should().BeTrue();
        metrics.SortOperationCount.Should().Be(1);
    }
    
    [Fact]
    public void ExecutionPlanAnalyzer_CountsOperationTypes()
    {
        // Arrange
        var leafNode = new ExecutionPlanNode(
            OperationType: "IndexSeek",
            RowsProduced: 100,
            RowsExamined: 100,
            Cost: 1.0m,
            ObjectName: "IX_Test",
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var rootNode = new ExecutionPlanNode(
            OperationType: "TableScan",
            RowsProduced: 1000,
            RowsExamined: 1000,
            Cost: 5.0m,
            ObjectName: "Table",
            Predicate: null,
            Children: new[] { leafNode },
            Properties: new Dictionary<string, object>()
        );
        
        var analyzer = new PublicTestAnalyzer();
        
        // Act
        var tableScans = analyzer.PublicCountOperationType(rootNode, "TableScan");
        var indexSeeks = analyzer.PublicCountOperationType(rootNode, "IndexSeek");
        
        // Assert
        tableScans.Should().Be(1);
        indexSeeks.Should().Be(1);
    }
    
    [Fact]
    public void ExecutionPlanAnalyzer_CalculatesTotalRowsExamined()
    {
        // Arrange
        var leaf1 = new ExecutionPlanNode(
            OperationType: "IndexSeek",
            RowsProduced: 50,
            RowsExamined: 50,
            Cost: 1.0m,
            ObjectName: null,
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var leaf2 = new ExecutionPlanNode(
            OperationType: "IndexSeek",
            RowsProduced: 75,
            RowsExamined: 75,
            Cost: 1.5m,
            ObjectName: null,
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var rootNode = new ExecutionPlanNode(
            OperationType: "NestedLoopJoin",
            RowsProduced: 125,
            RowsExamined: 125,
            Cost: 3.5m,
            ObjectName: null,
            Predicate: null,
            Children: new[] { leaf1, leaf2 },
            Properties: new Dictionary<string, object>()
        );
        
        var analyzer = new PublicTestAnalyzer();
        
        // Act
        var totalRows = analyzer.PublicCalculateTotalRowsExamined(rootNode);
        
        // Assert
        totalRows.Should().Be(250); // 125 + 50 + 75
    }
    
    [Theory]
    [InlineData(100, 100, 1.0)]
    [InlineData(50, 100, 0.5)]
    [InlineData(10, 1000, 0.01)]
    [InlineData(0, 1000, 0.0)]
    public void ExecutionPlanAnalyzer_CalculatesSelectivity(long rowsProduced, long rowsExamined, double expectedSelectivity)
    {
        // Arrange
        var analyzer = new PublicTestAnalyzer();
        
        // Act
        var selectivity = analyzer.PublicCalculateSelectivity(rowsProduced, rowsExamined);
        
        // Assert
        selectivity.Should().Be(expectedSelectivity);
    }
    
    [Fact]
    public void ExecutionPlanAnalyzer_ExtractsMissingIndexes()
    {
        // Arrange
        var node = new ExecutionPlanNode(
            OperationType: "TableScan",
            RowsProduced: 5000,
            RowsExamined: 5000,
            Cost: 10.0m,
            ObjectName: "Orders",
            Predicate: "Status = 'Active'",
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 10.0m,
            TotalRowsProduced: 5000,
            ExecutionTimeMs: 100,
            QueryText: "SELECT * FROM Orders WHERE Status = 'Active'",
            Metadata: new Dictionary<string, object>()
        );
        
        var analyzer = new PublicTestAnalyzer();
        
        // Act
        var recommendations = analyzer.PublicExtractMissingIndexes(plan);
        
        // Assert
        recommendations.Should().HaveCountGreaterThan(0);
        recommendations.First().Should().Contain("Orders");
    }
    
    [Fact]
    public void ExecutionPlanAnalyzer_GeneratesOptimizationTips()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalCost: 20.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 100000,
            TotalRowsProduced: 100000,
            Selectivity: 1.0,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 200,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        var analyzer = new PublicTestAnalyzer();
        
        // Act
        var tips = analyzer.PublicGenerateOptimizationTips(metrics);
        
        // Assert
        tips.Should().HaveCountGreaterThan(0);
        tips.Should().Contain(t => t.Contains("table scan", StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public void PerformanceMetrics_IncludesMissingIndexRecommendations()
    {
        // Arrange
        var recommendations = new List<string>
        {
            "CREATE INDEX idx_UserId ON Users(UserId)",
            "CREATE INDEX idx_Status ON Orders(Status)"
        };
        
        var metrics = new PerformanceMetrics(
            TotalCost: 15.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 50000,
            TotalRowsProduced: 100,
            Selectivity: 0.002,
            HasTableScan: true,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 150,
            MissingIndexRecommendations: recommendations,
            OptimizationTips: new List<string>()
        );
        
        // Act & Assert
        metrics.MissingIndexRecommendations.Should().HaveCount(2);
        metrics.MissingIndexRecommendations.Should().Contain("CREATE INDEX idx_UserId ON Users(UserId)");
    }
    
    [Fact]
    public void PerformanceMetrics_IncludesOptimizationTips()
    {
        // Arrange
        var tips = new List<string>
        {
            "Query contains full table scan - consider adding indexes",
            "Large sort operation detected - verify ORDER BY is necessary or add index"
        };
        
        var metrics = new PerformanceMetrics(
            TotalCost: 25.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 1,
            FilterOperationCount: 0,
            TotalRowsExamined: 100000,
            TotalRowsProduced: 50000,
            Selectivity: 0.5,
            HasTableScan: true,
            HasSort: true,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 250,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: tips
        );
        
        // Act & Assert
        metrics.OptimizationTips.Should().HaveCount(2);
        metrics.OptimizationTips.Should().Contain(t => t.Contains("table scan"));
    }
    
    // Test helper class - exposes protected methods for testing
    private class PublicTestAnalyzer : ExecutionPlanAnalyzer
    {
        public override PerformanceMetrics AnalyzePlan(string planJson, string queryText)
        {
            throw new NotImplementedException();
        }
        
        public override QueryExecutionPlan ParsePlan(string planOutput, string queryText)
        {
            throw new NotImplementedException();
        }
        
        public int PublicCountOperationType(ExecutionPlanNode node, string operationType)
            => CountOperationType(node, operationType);
        
        public long PublicCalculateTotalRowsExamined(ExecutionPlanNode node)
            => CalculateTotalRowsExamined(node);
        
        public double PublicCalculateSelectivity(long rowsProduced, long rowsExamined)
            => CalculateSelectivity(rowsProduced, rowsExamined);
        
        public List<string> PublicExtractMissingIndexes(QueryExecutionPlan plan)
            => ExtractMissingIndexes(plan);
        
        public List<string> PublicGenerateOptimizationTips(PerformanceMetrics metrics)
            => GenerateOptimizationTips(metrics);
    }
}
