using Dialect.Core.Performance;
using Dialect.PostgreSql.Optimization;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.Optimization;

public class PostgreSqlOptimizerTests
{
    private readonly PostgreSqlOptimizer _optimizer = new();

    private static QueryExecutionPlan CreateExecutionPlan(
        string operationType = "Seq Scan",
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
    public void GenerateRecommendations_IncludesAnalyzeForLowSelectivity()
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
        recommendations.Should().Contain(r => r.RecommendationId == "POSTGRES_ANALYZE");
    }

    [Fact]
    public void GenerateRecommendations_IncludesVacuumForLargeTable()
    {
        // Arrange
        var plan = CreateExecutionPlan(rowsExamined: 150000);
        var metrics = CreateMetrics(totalRowsExamined: 150000);

        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);

        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "POSTGRES_VACUUM");
    }

    [Fact]
    public void GenerateRecommendations_IncludesPartialIndexForFilteredQueries()
    {
        // Arrange
        var plan = CreateExecutionPlan(operationType: "Seq Scan", rowsExamined: 10000);
        var metrics = CreateMetrics(tableScanCount: 1, totalRowsExamined: 10000);
        const string queryWithWhere = "SELECT * FROM orders WHERE status = 'active' AND created_at > NOW() - INTERVAL '30 days'";

        // Act
        var recommendations = _optimizer.GenerateRecommendations(queryWithWhere, metrics, plan);

        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "POSTGRES_PARTIAL_INDEX");
    }

    [Fact]
    public void GenerateRecommendations_IncludesBrinForLargeSequentialTable()
    {
        // Arrange
        var plan = CreateExecutionPlan(rowsExamined: 2000000);
        var metrics = CreateMetrics(tableScanCount: 1, totalRowsExamined: 2000000);

        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);

        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "POSTGRES_BRIN");
    }

    [Fact]
    public void GenerateRecommendations_IncludesMaterializedViewForComplexQuery()
    {
        // Arrange
        var node = new ExecutionPlanNode(
            OperationType: "Nested Loop",
            RowsProduced: 50000,
            RowsExamined: 50000,
            Cost: 50.0m,
            ObjectName: "orders",
            Predicate: null,
            Children: new[]
            {
                new ExecutionPlanNode(
                    OperationType: "Nested Loop",
                    RowsProduced: 25000,
                    RowsExamined: 25000,
                    Cost: 25.0m,
                    ObjectName: "customers",
                    Predicate: null,
                    Children: Array.Empty<ExecutionPlanNode>(),
                    Properties: new Dictionary<string, object>()
                ),
                new ExecutionPlanNode(
                    OperationType: "Seq Scan",
                    RowsProduced: 10000,
                    RowsExamined: 10000,
                    Cost: 10.0m,
                    ObjectName: "items",
                    Predicate: null,
                    Children: Array.Empty<ExecutionPlanNode>(),
                    Properties: new Dictionary<string, object>()
                )
            },
            Properties: new Dictionary<string, object>()
        );

        var plan = new QueryExecutionPlan(
            RootNode: node,
            TotalCost: 50.0m,
            TotalRowsProduced: 50000,
            ExecutionTimeMs: 2000,
            QueryText: "SELECT * FROM orders o JOIN customers c ON o.customer_id = c.id JOIN items i ON o.item_id = i.id",
            Metadata: new Dictionary<string, object>()
        );

        var metrics = new PerformanceMetrics(
            TotalCost: 50.0m,
            TableScanCount: 0,
            IndexSeekCount: 0,
            IndexScanCount: 0,
            NestedLoopJoinCount: 3,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: 50000,
            TotalRowsProduced: 50000,
            Selectivity: 1.0,
            HasTableScan: false,
            HasSort: false,
            HasIneffectiveNestedLoop: false,
            ExecutionTimeMs: 2000,
            MissingIndexRecommendations: new List<string>(),
            OptimizationTips: new List<string>()
        );

        // Act
        var recommendations = _optimizer.GenerateRecommendations("SELECT * FROM orders", metrics, plan);

        // Assert
        recommendations.Should().Contain(r => r.RecommendationId == "POSTGRES_MATVIEW");
    }
}
