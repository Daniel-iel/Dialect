namespace Dialect.SqlServer.Performance;

using System.Text.Json;
using Dialect.Core.Performance;
using ExecutionPlanNode = Dialect.Core.Performance.ExecutionPlanNode;

/// <summary>
/// Analyzes SQL Server execution plans from JSON format (SQL Server 2016+).
/// Parses execution plan XML/JSON, extracts costs, and generates recommendations.
/// </summary>
public class SqlServerPlanAnalyzer : ExecutionPlanAnalyzer
{
    /// <summary>
    /// Analyzes a SQL Server execution plan in JSON format.
    /// </summary>
    public override PerformanceMetrics AnalyzePlan(string planJson, string queryText)
    {
        var plan = ParsePlan(planJson, queryText);
        return AnalyzeParsedPlan(plan);
    }
    
    /// <summary>
    /// Parses SQL Server execution plan JSON into structured format.
    /// SQL Server 2016+ provides machine-readable JSON format from SET STATISTICS IO/TIME.
    /// </summary>
    public override QueryExecutionPlan ParsePlan(string planOutput, string queryText)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(planOutput);
            var root = jsonDoc.RootElement;
            
            // Extract root execution plan node
            var rootNode = ParseNode(root.GetProperty("Root"));
            
            // Extract summary statistics
            var totalCost = root.TryGetProperty("EstimatedTotalSubtreeCost", out var costProp)
                ? decimal.Parse(costProp.GetString() ?? "0")
                : 0m;
            
            var totalRows = root.TryGetProperty("EstimatedRows", out var rowsProp)
                ? long.Parse(rowsProp.GetString() ?? "0")
                : 0L;
            
            var executionTime = root.TryGetProperty("ExecutionTime", out var timeProp)
                ? double.Parse(timeProp.GetString() ?? "0")
                : 0.0;
            
            return new QueryExecutionPlan(
                RootNode: rootNode,
                TotalCost: totalCost,
                TotalRowsProduced: totalRows,
                ExecutionTimeMs: executionTime,
                QueryText: queryText,
                Metadata: ExtractMetadata(root)
            );
        }
        catch
        {
            return CreateFallbackPlan(queryText);
        }
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
    
    private ExecutionPlanNode ParseNode(JsonElement nodeElement)
    {
        var operationType = nodeElement.TryGetProperty("RelOp", out var opProp)
            ? opProp.GetString() ?? "Unknown"
            : "Unknown";
        
        var rowsProduced = nodeElement.TryGetProperty("EstimatedRows", out var rowsProp)
            ? long.Parse(rowsProp.GetString() ?? "0")
            : 0L;
        
        var rowsExamined = rowsProduced; // SQL Server doesn't always distinguish examined vs produced
        
        var cost = nodeElement.TryGetProperty("EstimatedTotalSubtreeCost", out var costProp)
            ? decimal.Parse(costProp.GetString() ?? "0")
            : 0m;
        
        var objectName = GetObjectName(nodeElement);
        var predicate = GetPredicate(nodeElement);
        
        var children = new List<ExecutionPlanNode>();
        if (nodeElement.TryGetProperty("RelOp", out _) && 
            nodeElement.TryGetProperty("Child", out var childProp))
        {
            children.Add(ParseNode(childProp));
        }
        
        var properties = new Dictionary<string, object>();
        if (nodeElement.TryGetProperty("NodeId", out var nodeProp))
        {
            properties["NodeId"] = nodeProp.GetString() ?? "0";
        }
        
        return new ExecutionPlanNode(
            OperationType: operationType,
            RowsProduced: rowsProduced,
            RowsExamined: rowsExamined,
            Cost: cost,
            ObjectName: objectName,
            Predicate: predicate,
            Children: children,
            Properties: properties
        );
    }
    
    private static string? GetObjectName(JsonElement nodeElement)
    {
        if (nodeElement.TryGetProperty("Object", out var objProp) &&
            objProp.TryGetProperty("Table", out var tableProp))
        {
            return tableProp.GetString();
        }
        
        if (nodeElement.TryGetProperty("Object", out var objProp2) &&
            objProp2.TryGetProperty("Index", out var indexProp))
        {
            return indexProp.GetString();
        }
        
        return null;
    }
    
    private static string? GetPredicate(JsonElement nodeElement)
    {
        if (nodeElement.TryGetProperty("Predicate", out var predProp))
        {
            return predProp.GetString();
        }
        
        if (nodeElement.TryGetProperty("Where", out var whereProp))
        {
            return whereProp.GetString();
        }
        
        return null;
    }
    
    private static Dictionary<string, object> ExtractMetadata(JsonElement root)
    {
        var metadata = new Dictionary<string, object>();
        
        if (root.TryGetProperty("StatementOptLevel", out var levelProp))
        {
            metadata["OptLevel"] = levelProp.GetString() ?? "Unknown";
        }
        
        if (root.TryGetProperty("StatementSubTreeCost", out var costProp))
        {
            metadata["SubtreeCost"] = costProp.GetString() ?? "0";
        }
        
        return metadata;
    }
    
    private PerformanceMetrics AnalyzeParsedPlan(QueryExecutionPlan plan)
    {
        var tableScanCount = CountOperationType(plan.RootNode, "TableScan");
        var indexSeekCount = CountOperationType(plan.RootNode, "IndexSeek");
        var indexScanCount = CountOperationType(plan.RootNode, "IndexScan");
        var nestedLoopCount = CountOperationType(plan.RootNode, "NestedLoopJoin");
        var hashJoinCount = CountOperationType(plan.RootNode, "HashJoin");
        var sortCount = CountOperationType(plan.RootNode, "Sort");
        var filterCount = CountOperationType(plan.RootNode, "Filter");
        
        var totalRowsExamined = CalculateTotalRowsExamined(plan.RootNode);
        var selectivity = CalculateSelectivity(plan.TotalRowsProduced, totalRowsExamined);
        
        var missingIndexes = ExtractMissingIndexes(plan);
        var tips = GenerateOptimizationTips(new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: tableScanCount,
            IndexSeekCount: indexSeekCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: hashJoinCount,
            SortOperationCount: sortCount,
            FilterOperationCount: filterCount,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: tableScanCount > 0,
            HasSort: sortCount > 0,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: new List<string>()
        ));
        
        return new PerformanceMetrics(
            TotalCost: plan.TotalCost,
            TableScanCount: tableScanCount,
            IndexSeekCount: indexSeekCount,
            IndexScanCount: indexScanCount,
            NestedLoopJoinCount: nestedLoopCount,
            HashJoinCount: hashJoinCount,
            SortOperationCount: sortCount,
            FilterOperationCount: filterCount,
            TotalRowsExamined: totalRowsExamined,
            TotalRowsProduced: plan.TotalRowsProduced,
            Selectivity: selectivity,
            HasTableScan: tableScanCount > 0,
            HasSort: sortCount > 0,
            HasIneffectiveNestedLoop: nestedLoopCount > 0 && totalRowsExamined > 10000,
            ExecutionTimeMs: plan.ExecutionTimeMs,
            MissingIndexRecommendations: missingIndexes,
            OptimizationTips: tips
        );
    }
}
