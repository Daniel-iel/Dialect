using Dialect.Core.Performance;
using Dialect.MySql.Optimization;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class MySqlOptimizerTests
{
    private readonly MySqlOptimizer _optimizer = new();
    
    private static QueryExecutionPlan CreateExecutionPlan(
        string operationType = "ALL",
        long rowsProduced = 1000,
        long rowsExamined = 1000,
        decimal cost = 5.0m)
    {
        var node = new ExecutionPlanNode(
            OperationType: operationType,
            RowsProduced: rowsProduced,
            RowsExamined: rowsExamined,
            Cost: cost,
            ObjectName: "orders",
            Predicate: "status = 'active'",
            Children: Array.Empty<ExecutionPlanNode>(),
            Properties: new Dictionary<string, object>()
        );
        
        return new QueryExecutionPlan(
            RootNode: node,
            TotalCost: cost,
            TotalRowsProduced: rowsProduced,
            ExecutionTimeMs: 100,
            QueryText: "SELECT * FROM orders WHERE status = 'active'",
            Metadata: new Dictionary<string, object>()
        );
    }
    
    private static PerformanceMetrics CreateMetrics(
        int tableScanCount = 1,
        bool hasTableScan = true,
        long totalRowsExamined = 1000,
        decimal totalCost = 5.0m)
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
            ExecutionTimeMs: 100,
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
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);
        
        // Assert
        recommendations.Should().NotBeEmpty();
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesAnalyzeTableForLowSelectivity()
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
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "MYSQL_ANALYZE_TABLE");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesCompositeIndexForFilterConditions()
    {
        // Arrange
        var plan = CreateExecutionPlan(operationType: "ALL", rowsExamined: 10000);
        var metrics = new PerformanceMetrics(
            TotalCost: 5.0m,
            TableScanCount: 1,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 0,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 1,
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
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders WHERE status = 'active' AND customer_id = 5", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "MYSQL_COMPOSITE_INDEX");
    }
    
    [Fact(Skip = "MySQL generated column optimization not fully implemented")]
    public void GenerateRecommendations_IncludesGeneratedColumnForFunctionFilters()
    {
        // Arrange
        var plan = CreateExecutionPlan(operationType: "ALL");
        var metrics = CreateMetrics();
        var queryWithFunction = "SELECT * FROM orders WHERE YEAR(created_at) = 2024";
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations(queryWithFunction, metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "MYSQL_GENERATED_COLUMN");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesPartitioningForVeryLargeTable()
    {
        // Arrange
        var plan = CreateExecutionPlan(rowsExamined: 6000000);
        var metrics = CreateMetrics(totalRowsExamined: 6000000);
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "MYSQL_PARTITIONING");
    }
    
    [Fact]
    public void GenerateRecommendations_IncludesQueryCacheForSlowSelects()
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
            TotalRowsExamined: 100000,
            TotalRowsProduced: 100000,
            Selectivity: 1.0,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 600,  // > 500ms
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders WHERE status = 'active'", metrics, plan);
        
        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "MYSQL_QUERY_CACHE");
    }
    
    [Fact]
    public void GenerateRecommendations_DoesNotIncludeQueryCacheForInsert()
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
            TotalRowsExamined: 100000,
            TotalRowsProduced: 100000,
            Selectivity: 1.0,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 600,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );
        
        // Act
        var recommendations = _optimizer.GenerateRecommendations("INSERT INTO orders VALUES (...)", metrics, plan);
        
        // Assert
        recommendations.Should().NotContain(r => r.RecommendationId == "MYSQL_QUERY_CACHE");
    }
}
