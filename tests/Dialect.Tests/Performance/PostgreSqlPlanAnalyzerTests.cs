namespace Dialect.Tests.Performance;

using FluentAssertions;
using Xunit;
using Dialect.PostgreSql.Performance;

public class PostgreSqlPlanAnalyzerTests
{
    private readonly PostgreSqlPlanAnalyzer _analyzer = new();
    
    [Fact]
    public void ParsePlan_HandlesValidExplainAnalyzeJson()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Seq Scan",
                    "Relation Name": "users",
                    "Startup Cost": 0.00,
                    "Total Cost": 35.00,
                    "Plan Rows": 1000,
                    "Actual Rows": 1000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.123 ms",
                "Execution Time": "125.456 ms"
            }
        ]
        """;
        
        var queryText = "SELECT * FROM users";
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, queryText);
        
        // Assert
        plan.RootNode.OperationType.Should().Be("Seq Scan");
        plan.RootNode.ObjectName.Should().Be("users");
        plan.ExecutionTimeMs.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void ParsePlan_HandlesMissingActualRows()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Index Scan",
                    "Relation Name": "products",
                    "Startup Cost": 0.00,
                    "Total Cost": 10.00,
                    "Plan Rows": 50
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "50.000 ms"
            }
        ]
        """;
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM products WHERE id = 1");
        
        // Assert
        plan.RootNode.RowsProduced.Should().BeGreaterThanOrEqualTo(0);
    }
    
    [Fact]
    public void ParsePlan_HandlesInvalidJson()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var queryText = "SELECT * FROM users";
        
        // Act
        var plan = _analyzer.ParsePlan(invalidJson, queryText);
        
        // Assert
        plan.RootNode.OperationType.Should().Be("Unknown");
        plan.TotalCost.Should().Be(0);
    }
    
    [Fact]
    public void ParsePlan_ExtractsFilterPredicate()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Seq Scan",
                    "Relation Name": "users",
                    "Filter": "status = 'active'",
                    "Startup Cost": 0.00,
                    "Total Cost": 35.00,
                    "Plan Rows": 500,
                    "Actual Rows": 500,
                    "Actual Loops": 1
                },
                "Planning Time": "0.050 ms",
                "Execution Time": "20.000 ms"
            }
        ]
        """;
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM users WHERE status = 'active'");
        
        // Assert
        plan.RootNode.Predicate.Should().Be("status = 'active'");
    }
    
    [Fact]
    public void AnalyzePlan_CountsSequentialScans()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Seq Scan",
                    "Relation Name": "orders",
                    "Startup Cost": 0.00,
                    "Total Cost": 50.00,
                    "Plan Rows": 10000,
                    "Actual Rows": 10000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "100.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM orders");
        
        // Assert
        metrics.TableScanCount.Should().Be(1);
        metrics.HasTableScan.Should().BeTrue();
    }
    
    [Fact]
    public void AnalyzePlan_CountsIndexScans()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Index Scan",
                    "Relation Name": "users",
                    "Startup Cost": 0.00,
                    "Total Cost": 5.00,
                    "Plan Rows": 50,
                    "Actual Rows": 50,
                    "Actual Loops": 1
                },
                "Planning Time": "0.050 ms",
                "Execution Time": "5.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM users WHERE id = 1");
        
        // Assert
        metrics.IndexScanCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_CountsIndexOnlyScans()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Index Only Scan",
                    "Relation Name": "users",
                    "Startup Cost": 0.00,
                    "Total Cost": 1.00,
                    "Plan Rows": 10,
                    "Actual Rows": 10,
                    "Actual Loops": 1
                },
                "Planning Time": "0.050 ms",
                "Execution Time": "1.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT id FROM users WHERE id IN (1, 2, 3)");
        
        // Assert
        metrics.IndexSeekCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsNestedLoopJoins()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Nested Loop",
                    "Startup Cost": 0.00,
                    "Total Cost": 100.00,
                    "Plan Rows": 1000,
                    "Actual Rows": 1000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "50.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");
        
        // Assert
        metrics.NestedLoopJoinCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsHashJoins()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Hash Join",
                    "Startup Cost": 0.00,
                    "Total Cost": 50.00,
                    "Plan Rows": 2000,
                    "Actual Rows": 2000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "30.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");
        
        // Assert
        metrics.HashJoinCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsSortOperations()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Sort",
                    "Startup Cost": 10.00,
                    "Total Cost": 25.00,
                    "Plan Rows": 1000,
                    "Actual Rows": 1000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.050 ms",
                "Execution Time": "15.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM users ORDER BY name");
        
        // Assert
        metrics.SortOperationCount.Should().BeGreaterThan(0);
        metrics.HasSort.Should().BeTrue();
    }
    
    [Fact]
    public void AnalyzePlan_CalculatesSelectivity()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Seq Scan",
                    "Relation Name": "users",
                    "Filter": "status = 'active'",
                    "Startup Cost": 0.00,
                    "Total Cost": 35.00,
                    "Plan Rows": 100,
                    "Actual Rows": 100,
                    "Actual Loops": 1
                },
                "Planning Time": "0.050 ms",
                "Execution Time": "20.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM users WHERE status = 'active'");
        
        // Assert
        metrics.Selectivity.Should().BeLessThanOrEqualTo(1.0);
        metrics.Selectivity.Should().BeGreaterThan(0);
    }
    
    [Fact(Skip = "PostgreSQL execution plan analysis not fully implemented")]
    public void AnalyzePlan_RecommendsMissingIndexes()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Seq Scan",
                    "Relation Name": "orders",
                    "Filter": "customer_id = 5",
                    "Startup Cost": 0.00,
                    "Total Cost": 50.00,
                    "Plan Rows": 5000,
                    "Actual Rows": 5000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "50.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM orders WHERE customer_id = 5");
        
        // Assert
        metrics.MissingIndexRecommendations.Should().HaveCountGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_GeneratesOptimizationTips()
    {
        // Arrange
        var planJson = """
        [
            {
                "Plan": {
                    "Node Type": "Nested Loop",
                    "Startup Cost": 0.00,
                    "Total Cost": 100.00,
                    "Plan Rows": 50000,
                    "Actual Rows": 50000,
                    "Actual Loops": 1
                },
                "Planning Time": "0.100 ms",
                "Execution Time": "200.000 ms"
            }
        ]
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");
        
        // Assert
        metrics.OptimizationTips.Should().HaveCountGreaterThan(0);
    }
}
