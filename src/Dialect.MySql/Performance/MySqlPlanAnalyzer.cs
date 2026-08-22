namespace Dialect.MySql.Performance;

using System.Text.Json;
using System.Text.RegularExpressions;
using Dialect.Core.Performance;
using ExecutionPlanNode = Dialect.Core.Performance.ExecutionPlanNode;

/// <summary>
/// Analyzes MySQL execution plans from EXPLAIN JSON format.
/// Parses plan output, extracts access methods and row estimates, and generates recommendations.
/// </summary>
public class MySqlPlanAnalyzer : ExecutionPlanAnalyzer
{
    /// <summary>
    /// Analyzes a MySQL execution plan in JSON format (EXPLAIN FORMAT=JSON).
    /// </summary>
    public override PerformanceMetrics AnalyzePlan(string planJson, string queryText)
    {
        var plan = ParsePlan(planJson, queryText);
        return AnalyzeParsedPlan(plan);
    }
    
    /// <summary>
    /// Parses MySQL execution plan JSON into structured format.
    /// MySQL EXPLAIN FORMAT=JSON provides access method, rows read/sent, and cost information.
    /// </summary>
    public override QueryExecutionPlan ParsePlan(string planOutput, string queryText)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(planOutput);
            var root = jsonDoc.RootElement;
            
            if (root.TryGetProperty("query_block", out var queryBlockProp))
            {
                var rootNode = ParseNode(queryBlockProp);
                
                // Extract query statistics if available
                var executionTime = root.TryGetProperty("query_time", out var timeProp)
                    ? double.Parse(timeProp.GetString()?.Trim() ?? "0")
                    : 0.0;
                
                return new QueryExecutionPlan(
                    RootNode: rootNode,
                    TotalCost: CalculateNodeCost(rootNode),
                    TotalRowsProduced: rootNode.RowsProduced,
                    ExecutionTimeMs: executionTime,
                    QueryText: queryText,
                    Metadata: ExtractMetadata(root)
                );
            }
            
            return CreateFallbackPlan(queryText);
        }
        catch
        {
            return CreateFallbackPlan(queryText);
        }
    }
    
    private static ExecutionPlanNode ParseNode(JsonElement nodeElement)
    {
        var operationType = "Unknown";
        var objectName = "";
        var rowsProduced = 0L;
        var rowsExamined = 0L;
        var predicate = "";
        
        // Extract table information
        if (nodeElement.TryGetProperty("table", out var tableProp))
        {
            if (tableProp.TryGetProperty("table_name", out var tableNameProp))
            {
                objectName = tableNameProp.GetString() ?? "";
            }
            
            if (tableProp.TryGetProperty("access_type", out var accessTypeProp))
            {
                operationType = accessTypeProp.GetString() ?? "Unknown";
            }
            
            if (tableProp.TryGetProperty("rows_examined_per_scan", out var rowsExamProp))
            {
                rowsExamined = rowsExamProp.GetInt64();
            }
            
            if (tableProp.TryGetProperty("rows_produced_per_join", out var rowsProdProp))
            {
                rowsProduced = rowsProdProp.GetInt64();
            }
            
            if (tableProp.TryGetProperty("attached_condition", out var condProp))
            {
                predicate = condProp.GetString() ?? "";
            }
        }
        
        // Fallback to defaults
        if (rowsExamined == 0)
            rowsExamined = rowsProduced;
        
        var children = new List<ExecutionPlanNode>();
        
        // Parse nested selects
        if (nodeElement.TryGetProperty("select_list", out var selectListProp) &&
            selectListProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var selectItem in selectListProp.EnumerateArray())
            {
                if (selectItem.TryGetProperty("select_id", out _))
                {
                    children.Add(ParseNode(selectItem));
                }
            }
        }
        
        var properties = new Dictionary<string, object>();
        if (nodeElement.TryGetProperty("table", out var tableProp2))
        {
            if (tableProp2.TryGetProperty("key", out var keyProp))
            {
                properties["Key"] = keyProp.GetString() ?? "None";
            }
            if (tableProp2.TryGetProperty("possible_keys", out var possibleKeysProp))
            {
                properties["PossibleKeys"] = possibleKeysProp.GetString() ?? "None";
            }
        }
        
        return new ExecutionPlanNode(
            OperationType: operationType,
            RowsProduced: rowsProduced,
            RowsExamined: rowsExamined,
            Cost: 0, // MySQL doesn't provide explicit cost in EXPLAIN
            ObjectName: objectName,
            Predicate: string.IsNullOrEmpty(predicate) ? null : predicate,
            Children: children,
            Properties: properties
        );
    }
    
    private static decimal CalculateNodeCost(ExecutionPlanNode node)
    {
        decimal cost = 0;
        var queue = new Queue<ExecutionPlanNode>();
        queue.Enqueue(node);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            // Estimate cost as rows_examined / 1000 (rough approximation)
            cost += decimal.Parse(Math.Max(1, current.RowsExamined / 1000).ToString());
            
            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
        
        return cost;
    }
    
    private static Dictionary<string, object> ExtractMetadata(JsonElement root)
    {
        var metadata = new Dictionary<string, object>();
        
        if (root.TryGetProperty("query_time", out var queryTimeProp))
        {
            metadata["QueryTime"] = queryTimeProp.GetString() ?? "0";
        }
        
        return metadata;
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
        var fullTableScanCount = CountOperationType(plan.RootNode, "ALL");
        var indexSeekCount = CountOperationType(plan.RootNode, "const") + 
                             CountOperationType(plan.RootNode, "eq_ref") +
                             CountOperationType(plan.RootNode, "ref");
        var indexScanCount = CountOperationType(plan.RootNode, "range") + 
                             CountOperationType(plan.RootNode, "index");
        var nestedLoopCount = 1; // MySQL uses nested loop by default for joins
        
        var totalRowsExamined = CalculateTotalRowsExamined(plan.RootNode);
        var selectivity = CalculateSelectivity(plan.TotalRowsProduced, totalRowsExamined);
        
        var missingIndexes = ExtractMissingIndexes(plan);
        var tips = GenerateOptimizationTips(new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: fullTableScanCount,
            IndexSeekCount: indexSeekCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: 0, // Rarely used in MySQL
            SortOperationCount: 0, // Would need to check for "Using filesort"
            FilterOperationCount: 0,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: fullTableScanCount > 0,
            HasSort: false,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: new List<string>()
        ));
        
        return new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: fullTableScanCount,
            IndexSeekCount: indexSeekCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: 0,
            SortOperationCount: 0,
            FilterOperationCount: 0,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: fullTableScanCount > 0,
            HasSort: false,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: tips
        );
    }
}
