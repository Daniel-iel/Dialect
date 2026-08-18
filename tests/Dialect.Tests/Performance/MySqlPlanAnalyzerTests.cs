namespace Dialect.Tests.Performance;

using FluentAssertions;
using Xunit;
using Dialect.MySql.Performance;

public class MySqlPlanAnalyzerTests
{
    private readonly MySqlPlanAnalyzer _analyzer = new();
    
    [Fact]
    public void ParsePlan_HandlesValidExplainFormatJson()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "users",
                    "access_type": "ALL",
                    "rows_examined_per_scan": 1000,
                    "rows_produced_per_join": 1000
                }
            },
            "query_time": "0.125"
        }
        """;
        
        var queryText = "SELECT * FROM users";
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, queryText);
        
        // Assert
        plan.RootNode.OperationType.Should().Be("ALL");
        plan.RootNode.ObjectName.Should().Be("users");
        plan.ExecutionTimeMs.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void ParsePlan_HandlesMissingOptionalFields()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "products",
                    "access_type": "index"
                }
            }
        }
        """;
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM products");
        
        // Assert
        plan.RootNode.ObjectName.Should().Be("products");
        plan.RootNode.OperationType.Should().Be("index");
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
    public void ParsePlan_ExtractsAttachedCondition()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "users",
                    "access_type": "ALL",
                    "rows_examined_per_scan": 500,
                    "rows_produced_per_join": 500,
                    "attached_condition": "users.status = 'active'"
                }
            }
        }
        """;
        
        // Act
        var plan = _analyzer.ParsePlan(planJson, "SELECT * FROM users WHERE status = 'active'");
        
        // Assert
        plan.RootNode.Predicate.Should().Be("users.status = 'active'");
    }
    
    [Fact]
    public void AnalyzePlan_CountsFullTableScans()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "orders",
                    "access_type": "ALL",
                    "rows_examined_per_scan": 10000,
                    "rows_produced_per_join": 10000
                }
            },
            "query_time": "0.100"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM orders");
        
        // Assert
        metrics.TableScanCount.Should().Be(1);
        metrics.HasTableScan.Should().BeTrue();
    }
    
    [Fact]
    public void AnalyzePlan_DetectsConstAccess()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "users",
                    "access_type": "const",
                    "key": "PRIMARY",
                    "rows_examined_per_scan": 1,
                    "rows_produced_per_join": 1
                }
            },
            "query_time": "0.005"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM users WHERE id = 1");
        
        // Assert
        metrics.IndexSeekCount.Should().BeGreaterThan(0);
        metrics.TableScanCount.Should().Be(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsEqRefAccess()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "orders",
                    "access_type": "eq_ref",
                    "key": "PRIMARY",
                    "rows_examined_per_scan": 100,
                    "rows_produced_per_join": 100
                }
            },
            "query_time": "0.010"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM orders WHERE id = 1");
        
        // Assert
        metrics.IndexSeekCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsRefAccess()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "items",
                    "access_type": "ref",
                    "key": "fk_order_id",
                    "rows_examined_per_scan": 50,
                    "rows_produced_per_join": 50
                }
            },
            "query_time": "0.008"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM items WHERE order_id = 123");
        
        // Assert
        metrics.IndexSeekCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_DetectsRangeAccess()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "products",
                    "access_type": "range",
                    "key": "idx_price",
                    "rows_examined_per_scan": 500,
                    "rows_produced_per_join": 500
                }
            },
            "query_time": "0.050"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM products WHERE price > 100");
        
        // Assert
        metrics.IndexScanCount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void AnalyzePlan_CalculatesSelectivity()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "users",
                    "access_type": "ALL",
                    "rows_examined_per_scan": 100000,
                    "rows_produced_per_join": 50
                }
            },
            "query_time": "0.200"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM users WHERE status = 'active'");
        
        // Assert
        metrics.Selectivity.Should().BeLessThan(0.001);
    }
    
    [Fact(Skip = "MySQL execution plan analysis not fully implemented")]
    public void AnalyzePlan_RecommendsMissingIndexes()
    {
        // Arrange
        var planJson = """
        {
            "query_block": {
                "table": {
                    "table_name": "orders",
                    "access_type": "ALL",
                    "rows_examined_per_scan": 5000,
                    "rows_produced_per_join": 5000,
                    "attached_condition": "customer_id = 123"
                }
            },
            "query_time": "0.150"
        }
        """;
        
        // Act
        var metrics = _analyzer.AnalyzePlan(planJson, "SELECT * FROM orders WHERE customer_id = 123");
        
        // Assert
        metrics.MissingIndexRecommendations.Should().HaveCountGreaterThan(0);
    }
}
