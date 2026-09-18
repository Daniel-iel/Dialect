namespace Dialect.Tests.Performance;

using FluentAssertions;
using Xunit;
using Dialect.SqlServer.Performance;

public class SqlServerPlanAnalyzerTests
{
    private readonly SqlServerPlanAnalyzer _analyzer = new();

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void ParsePlan_HandlesValidJsonPlan()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "Users"},
                "EstimatedRows": 1000,
                "EstimatedTotalSubtreeCost": "5.5"
            },
            "EstimatedTotalSubtreeCost": "5.5",
            "EstimatedRows": 1000,
            "ExecutionTime": 125.5
        }
        """;

        const string queryText = "SELECT * FROM Users";

        // Act
        var plan = _analyzer.ParsePlan(planJson, queryText);

        // Assert
        plan.RootNode.OperationType.Should().Be("TableScan");
        plan.TotalCost.Should().Be(5.5m);
        plan.TotalRowsProduced.Should().Be(1000);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void ParsePlan_HandlesInvalidJson()
    {
        // Arrange
        const string invalidJson = "{ invalid json }";
        const string queryText = "SELECT * FROM Users";

        // Act
        var plan = _analyzer.ParsePlan(invalidJson, queryText);

        // Assert
        plan.RootNode.OperationType.Should().Be("Unknown");
        plan.TotalCost.Should().Be(0);
        plan.TotalRowsProduced.Should().Be(0);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void ParsePlan_ExtractsObjectName()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "IndexSeek",
                "Object": {"Index": "IX_UserId"},
                "EstimatedRows": 100,
                "EstimatedTotalSubtreeCost": "1.0"
            },
            "EstimatedTotalSubtreeCost": "1.0",
            "EstimatedRows": 100
        }
        """;

        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM Users WHERE UserId = 5");

        // Assert
        plan.RootNode.ObjectName.Should().Be("IX_UserId");
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void ParsePlan_ExtractsPredicate()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "Users"},
                "Predicate": "[Users].[Status] = 'Active'",
                "EstimatedRows": 500,
                "EstimatedTotalSubtreeCost": "3.0"
            },
            "EstimatedTotalSubtreeCost": "3.0",
            "EstimatedRows": 500
        }
        """;

        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM Users WHERE Status = 'Active'");

        // Assert
        plan.RootNode.Predicate.Should().Be("[Users].[Status] = 'Active'");
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_CountsTableScans()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "Orders"},
                "EstimatedRows": 10000,
                "EstimatedTotalSubtreeCost": "15.5"
            },
            "EstimatedTotalSubtreeCost": "15.5",
            "EstimatedRows": 10000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM Orders");

        // Assert
        metrics.TableScanCount.Should().Be(1);
        metrics.HasTableScan.Should().BeTrue();
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_CountsIndexSeeks()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "IndexSeek",
                "Object": {"Index": "IX_ProductId"},
                "EstimatedRows": 50,
                "EstimatedTotalSubtreeCost": "0.5"
            },
            "EstimatedTotalSubtreeCost": "0.5",
            "EstimatedRows": 50
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM Products WHERE ProductId = 1");

        // Assert
        metrics.IndexSeekCount.Should().Be(1);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_DetectsNestedLoopJoins()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "NestedLoopJoin",
                "EstimatedRows": 500,
                "EstimatedTotalSubtreeCost": "10.0"
            },
            "EstimatedTotalSubtreeCost": "10.0",
            "EstimatedRows": 500
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");

        // Assert
        metrics.NestedLoopJoinCount.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_DetectsHashJoins()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "HashJoin",
                "EstimatedRows": 1000,
                "EstimatedTotalSubtreeCost": "8.0"
            },
            "EstimatedTotalSubtreeCost": "8.0",
            "EstimatedRows": 1000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");

        // Assert
        metrics.HashJoinCount.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_DetectsSortOperations()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "Sort",
                "EstimatedRows": 5000,
                "EstimatedTotalSubtreeCost": "20.0"
            },
            "EstimatedTotalSubtreeCost": "20.0",
            "EstimatedRows": 5000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM Users ORDER BY Name");

        // Assert
        metrics.SortOperationCount.Should().BeGreaterThan(0);
        metrics.HasSort.Should().BeTrue();
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_CalculatesSelectivity()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "Users"},
                "EstimatedRows": 50,
                "EstimatedTotalSubtreeCost": "10.0"
            },
            "EstimatedTotalSubtreeCost": "10.0",
            "EstimatedRows": 50
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM Users WHERE Status = 'Active'");

        // Assert
        metrics.Selectivity.Should().BeLessThan(1.0);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_FlagsCostlyTableScans()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "LargeTable"},
                "EstimatedRows": 1000000,
                "EstimatedTotalSubtreeCost": "100.0"
            },
            "EstimatedTotalSubtreeCost": "100.0",
            "EstimatedRows": 1000000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM LargeTable");

        // Assert
        metrics.TableScanCount.Should().Be(1);
        metrics.OptimizationTips.Should().Contain(t => t.Contains("table scan"));
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_RecommendsMissingIndexes()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "TableScan",
                "Object": {"Table": "Orders"},
                "Predicate": "[Orders].[Status] = 'Active'",
                "EstimatedRows": 5000,
                "EstimatedTotalSubtreeCost": "25.0"
            },
            "EstimatedTotalSubtreeCost": "25.0",
            "EstimatedRows": 5000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM Orders WHERE Status = 'Active'");

        // Assert
        metrics.MissingIndexRecommendations.Should().HaveCountGreaterThan(0);
    }

    [Fact(Skip = "SQL Server execution plan JSON parsing not fully implemented")]
    public void AnalyzePlan_GeneratesOptimizationTips()
    {
        // Arrange
        const string planJson = """
        {
            "Root": {
                "RelOp": "NestedLoopJoin",
                "EstimatedRows": 50000,
                "EstimatedTotalSubtreeCost": "50.0"
            },
            "EstimatedTotalSubtreeCost": "50.0",
            "EstimatedRows": 50000
        }
        """;

        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id");

        // Assert
        metrics.OptimizationTips.Should().HaveCountGreaterThan(0);
    }
}
