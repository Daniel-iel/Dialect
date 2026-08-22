using Dialect.Core.Performance;
using Dialect.SqlServer.Optimization;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class SqlServerOptimizerTests
{
    private readonly SqlServerOptimizer _optimizer = new();
    
    private static QueryExecutionPlan CreateExecutionPlan(
        string operationType = "TableScan",
        long rowsProduced = 1000,
        long rowsExamined = 1000,
        decimal cost = 5.0m)
    {
        var node = new ExecutionPlanNode(
            OperationType: operationType,
            RowsProduced: rowsProduced,
            RowsExamined: rowsExamined,
            Cost: cost,
            ObjectName: "Orders",
            Predicate: "Status = 'Active'",
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        return new QueryExecutionPlan(
            RootNode: node,
            TotalCost: cost,
            TotalRowsProduced: rowsProduced,
            ExecutionTimeMs: 100,
            QueryText: "SELECT * FROM Orders WHERE Status = 'Active'",
            Metadata: new Dictionary<string, object>()
        );
    }
    
    private static PerformanceMetrics CreateMetrics(
        int tableScanCount = 1,
        bool hasTableScan = true,
        long totalRowsExamined = 1000,
        decimal totalCost = 5.0m,
        double executionTimeMs = 100)
    {
        return new PerformanceMetrics(
            TotalCost: totalCost,
            TableScanCount: tableScanCount,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: totalRowsExamined,
            Selectivity: 1.0,
            HasTableScan: hasTableScan,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: executionTimeMs,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
    }
    
    [Fact]
    public void GenerateRecommendations_ReturnsRecommendations()
    {
        // Arrange
        var plan = CreateExecutionPlan();
        var metrics = CreateMetrics();
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        
        // Assert
        recommendations.Should().NotBeEmpty();
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesIndexRecommendationForTableScan()
    {
        // Arrange
        var plan = CreateExecutionPlan("TableScan", rowsExamined: 10000);
        var metrics = CreateMetrics(tableScanCount: 1, totalRowsExamined: 10000);
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.Category == "Index");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesCoveringIndexForHighRows()
    {
        // Arrange
        var node = new ExecutionPlanNode(
            OperationType: "IndexSeek",
            RowsProduced: 15000,
            RowsExamined: 15000,
            Cost: 2.0m,
            ObjectName: "Orders",
            Predicate: null,
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 2.0m,
            TotalRowsProduced: 15000,
            ExecutionTimeMs: 150,
            QueryText: "SELECT * FROM Orders WHERE CustomerId = 5",
            Metadata: new Dictionary<string, object>()
        );
        
        var metrics = new PerformanceMetrics(
            TotalCost: 2.0m,
            TableScanCount: 0,
            IndexSeekCount: 1,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 15000,
            TotalRowsProduced: 15000,
            Selectivity: 1.0,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 150,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "INDEX_COVERING");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesParallelismForHighCost()
    {
        // Arrange
        var plan = CreateExecutionPlan(cost: 100);
        var metrics = CreateMetrics(totalCost: 100, executionTimeMs: 1000);
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "SQLSERVER_PARALLELISM");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesUpdateStatsForLowSelectivity()
    {
        // Arrange
        var plan = CreateExecutionPlan();
        var metrics = new PerformanceMetrics(
            TotalCost: 5.0m,
            TableScanCount: 0,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 1000,
            TotalRowsProduced: 10,
            Selectivity: 0.01,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 100,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders WHERE CustomerId > 5000", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "SQLSERVER_UPDATE_STATS");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesColumnstoreForVeryLargeTable()
    {
        // Arrange
        var plan = CreateExecutionPlan(rowsExamined: 2000000);
        var metrics = CreateMetrics(totalRowsExamined: 2000000);
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "SQLSERVER_COLUMNSTORE");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesTempTableWarningForDeclaredVariables()
    {
        // Arrange
        var plan = CreateExecutionPlan();
        var metrics = CreateMetrics();
        var queryWithVariable = "DECLARE @tempTable TABLE (id INT); SELECT * FROM Orders";
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations(queryWithVariable, metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "SQLSERVER_TEMP_TABLE");
    }
    
    [Fact]
    public void GenerateRecommendations_ReturnsOrderedByPriority()
    {
        // Arrange
        var plan = CreateExecutionPlan(rowsExamined: 10000, cost: 100);
        var metrics = CreateMetrics(totalRowsExamined: 10000, totalCost: 100, executionTimeMs: 1000);
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM Orders WHERE Status = 'Active'", metrics, plan);
        
        // Assert
        var priorityList = recommendations.Select(r => r.Priority).ToList();
        priorityList.Should().BeInDescendingOrder();
    }
}
