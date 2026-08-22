namespace Dialect.PostgreSql.Performance;

using System.Text.Json;
using Dialect.Core.Performance;
using ExecutionPlanNode = Dialect.Core.Performance.ExecutionPlanNode;

/// <summary>
/// Analyzes PostgreSQL execution plans from EXPLAIN ANALYZE JSON format.
/// Parses plan nodes, extracts costs and timing, and generates recommendations.
/// </summary>
public class PostgreSqlPlanAnalyzer : ExecutionPlanAnalyzer
{
    /// <summary>
    /// Analyzes a PostgreSQL execution plan in JSON format (EXPLAIN FORMAT JSON).
    /// </summary>
    public override PerformanceMetrics AnalyzePlan(string planJson, string queryText)
    {
        var plan = ParsePlan(planJson, queryText);
        return AnalyzeParsedPlan(plan);
    }
    
    /// <summary>
    /// Parses PostgreSQL execution plan JSON into structured format.
    /// PostgreSQL EXPLAIN ANALYZE provides detailed JSON output with actual vs. estimated costs.
    /// </summary>
    public override QueryExecutionPlan ParsePlan(string planOutput, string queryText)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(planOutput);
            var root = jsonDoc.RootElement;
            
            // PostgreSQL wraps plan in an array
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var planElement = root[0];
                
                if (planElement.TryGetProperty("Plan", out var planProp))
                {
                    var rootNode = ParseNode(planProp);
                    
                    var planningTime = planElement.TryGetProperty("Planning Time", out var planningProp)
                        ? double.Parse(planningProp.GetString()?.Replace(" ms", "").Trim() ?? "0")
                        : 0.0;
                    
                    var executionTime = planElement.TryGetProperty("Execution Time", out var execProp)
                        ? double.Parse(execProp.GetString()?.Replace(" ms", "").Trim() ?? "0")
                        : 0.0;
                    
                    return new QueryExecutionPlan(
                        RootNode: rootNode,
                        TotalCost: GetNodeCost(planProp),
                        TotalRowsProduced: GetNodeRows(planProp),
                        ExecutionTimeMs: executionTime,
                        QueryText: queryText,
                        Metadata: new Dictionary<string, object>
                        {
                            ["PlanningTime"] = planningTime
                        }
                    );
                }
            }
            
            // Fallback if different structure
            return CreateFallbackPlan(queryText);
        }
        catch
        {
            return CreateFallbackPlan(queryText);
        }
    }
    
    private static ExecutionPlanNode ParseNode(JsonElement nodeElement)
    {
        var nodeType = nodeElement.TryGetProperty("Node Type", out var typeProp)
            ? typeProp.GetString() ?? "Unknown"
            : "Unknown";
        
        var planRows = nodeElement.TryGetProperty("Plan Rows", out var planRowsProp)
            ? long.Parse(planRowsProp.GetInt64().ToString())
            : 0L;
        
        var actualRows = nodeElement.TryGetProperty("Actual Rows", out var actualRowsProp)
            ? long.Parse(actualRowsProp.GetInt64().ToString())
            : planRows;
        
        var planCost = nodeElement.TryGetProperty("Total Cost", out var costProp)
            ? decimal.Parse(costProp.GetDecimal().ToString())
            : 0m;
        
        var relationName = nodeElement.TryGetProperty("Relation Name", out var relProp)
            ? relProp.GetString()
            : null;
        
        var filter = nodeElement.TryGetProperty("Filter", out var filterProp)
            ? filterProp.GetString()
            : null;
        
        var children = new List<ExecutionPlanNode>();
        if (nodeElement.TryGetProperty("Plans", out var plansProp) &&
            plansProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var childElement in plansProp.EnumerateArray())
            {
                children.Add(ParseNode(childElement));
            }
        }
        
        var properties = new Dictionary<string, object>();
        if (nodeElement.TryGetProperty("Actual Loops", out var loopsProp))
        {
            properties["ActualLoops"] = loopsProp.GetInt64();
        }
        if (nodeElement.TryGetProperty("Startup Cost", out var startupProp))
        {
            properties["StartupCost"] = startupProp.GetDecimal();
        }
        
        return new ExecutionPlanNode(
            OperationType: nodeType,
            RowsProduced: actualRows,
            RowsExamined: actualRows * (nodeElement.TryGetProperty("Actual Loops", out var lp) ? (int)lp.GetInt64() : 1),
            Cost: planCost,
            ObjectName: relationName,
            Predicate: filter,
            Children: children,
            Properties: properties
        );
    }
    
    private static decimal GetNodeCost(JsonElement nodeElement)
    {
        if (nodeElement.TryGetProperty("Total Cost", out var costProp))
        {
            return costProp.GetDecimal();
        }
        return 0m;
    }
    
    private static long GetNodeRows(JsonElement nodeElement)
    {
        // Prefer actual rows from ANALYZE, fall back to plan rows
        if (nodeElement.TryGetProperty("Actual Rows", out var actualRowsProp))
        {
            return actualRowsProp.GetInt64();
        }
        
        if (nodeElement.TryGetProperty("Plan Rows", out var planRowsProp))
        {
            return planRowsProp.GetInt64();
        }
        
        return 0L;
    }
    
    private static QueryExecutionPlan CreateFallbackPlan(string queryText)
    {
        return new QueryExecutionPlan(
            RootNode: new ExecutionPlanNode(
                OperationType: "Unknown",
                RowsProduced: 0,
                RowsExamined: 0,
                Cost: 0,
                ObjectName: null,
                Predicate: null,
                Children: Array.Empty<ExecutionPlanNode>(),
                Properties: new Dictionary<string, object>()
            ),
            TotalCost: 0,
            TotalRowsProduced: 0,
            ExecutionTimeMs: 0,
            QueryText: queryText,
            Metadata: new Dictionary<string, object>()
        );
    }
    
    private PerformanceMetrics AnalyzeParsedPlan(QueryExecutionPlan plan)
    {
        var seqScanCount = CountOperationType(plan.RootNode, "Seq Scan");
        var indexScanCount = CountOperationType(plan.RootNode, "Index Scan");
        var indexOnlyScanCount = CountOperationType(plan.RootNode, "Index Only Scan");
        var nestedLoopCount = CountOperationType(plan.RootNode, "Nested Loop");
        var hashJoinCount = CountOperationType(plan.RootNode, "Hash Join");
        var mergeJoinCount = CountOperationType(plan.RootNode, "Merge Join");
        var sortCount = CountOperationType(plan.RootNode, "Sort");
        var filterCount = CountOperationType(plan.RootNode, "Filter");
        
        var totalRowsExamined = CalculateTotalRowsExamined(plan.RootNode);
        var selectivity = CalculateSelectivity(plan.TotalRowsProduced, totalRowsExamined);
        
        var missingIndexes = ExtractMissingIndexes(plan);
        var tips = GenerateOptimizationTips(new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: seqScanCount,
            IndexSeekCount: indexOnlyScanCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: hashJoinCount,
            SortOperationCount: sortCount,
            FilterOperationCount: filterCount,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: seqScanCount > 0,
            HasSort: sortCount > 0,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: new List<string>()
        ));
        
        return new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: seqScanCount,
            IndexSeekCount: indexOnlyScanCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: hashJoinCount,
            SortOperationCount: sortCount,
            FilterOperationCount: filterCount,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: seqScanCount > 0,
            HasSort: sortCount > 0,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: tips
        );
    }
}
