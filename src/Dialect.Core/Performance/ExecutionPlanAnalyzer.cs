namespace Dialect.Core.Performance;

/// <summary>
/// Base class for analyzing execution plans from database queries.
/// Provides common analysis methods that all dialect-specific analyzers implement.
/// </summary>
public abstract class ExecutionPlanAnalyzer
{
    /// <summary>
    /// Analyzes a raw execution plan output from a database and returns structured metrics.
    /// </summary>
    /// <param name="planJson">JSON representation of the execution plan (dialect-specific format)</param>
    /// <param name="queryText">Original query text for context</param>
    /// <returns>Analyzed performance metrics</returns>
    public abstract PerformanceMetrics AnalyzePlan(string planJson, string queryText);
    
    /// <summary>
    /// Parses raw plan output into a structured ExecutionPlan object.
    /// </summary>
    /// <param name="planOutput">Raw plan output from database query</param>
    /// <param name="queryText">Original query text</param>
    /// <returns>Structured execution plan</returns>
    public abstract QueryExecutionPlan ParsePlan(string planOutput, string queryText);
    
    /// <summary>
    /// Counts occurrences of a specific operation type in the execution plan tree.
    /// </summary>
    /// <param name="node">Root node to start counting from</param>
    /// <param name="operationType">Operation type to count (TableScan, IndexSeek, etc.)</param>
    /// <returns>Number of occurrences</returns>
    protected int CountOperationType(ExecutionPlanNode node, string operationType)
    {
        int count = 0;
        var queue = new Queue<ExecutionPlanNode>();
        queue.Enqueue(node);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
            
            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Calculates total rows examined across the entire execution plan tree.
    /// </summary>
    /// <param name="node">Root node to start calculating from</param>
    /// <returns>Total rows examined</returns>
    protected long CalculateTotalRowsExamined(ExecutionPlanNode node)
    {
        long total = node.RowsExamined;
        
        foreach (var child in node.Children)
        {
            total += CalculateTotalRowsExamined(child);
        }
        
        return total;
    }
    
    /// <summary>
    /// Calculates selectivity as a ratio of rows produced to rows examined.
    /// </summary>
    /// <param name="rowsProduced">Number of rows in final result</param>
    /// <param name="rowsExamined">Number of rows examined during execution</param>
    /// <returns>Selectivity ratio (0.0 to 1.0)</returns>
    protected double CalculateSelectivity(long rowsProduced, long rowsExamined)
    {
        if (rowsExamined == 0)
            return 1.0;
        
        return Math.Min(1.0, (double)rowsProduced / rowsExamined);
    }
    
    /// <summary>
    /// Extracts missing index recommendations from the execution plan.
    /// </summary>
    /// <param name="plan">Structured execution plan</param>
    /// <returns>List of missing index recommendations</returns>
    protected List<string> ExtractMissingIndexes(QueryExecutionPlan plan)
    {
        var recommendations = new List<string>();
        ExtractMissingIndexesRecursive(plan.RootNode, recommendations);
        return recommendations;
    }
    
    private void ExtractMissingIndexesRecursive(
        ExecutionPlanNode node,
        List<string> recommendations)
    {
        // Check for table scans or index scans with high row counts
        if ((node.OperationType.Equals("TableScan", StringComparison.OrdinalIgnoreCase) ||
             node.OperationType.Equals("IndexScan", StringComparison.OrdinalIgnoreCase)) &&
            node.RowsExamined > 1000 && !string.IsNullOrEmpty(node.ObjectName))
        {
            var predicate = !string.IsNullOrEmpty(node.Predicate) 
                ? $" with predicate '{node.Predicate}'" 
                : "";
            recommendations.Add($"Consider adding index on {node.ObjectName}{predicate} (scanned {node.RowsExamined} rows)");
        }
        
        foreach (var child in node.Children)
        {
            ExtractMissingIndexesRecursive(child, recommendations);
        }
    }
    
    /// <summary>
    /// Generates optimization tips based on the execution plan analysis.
    /// </summary>
    /// <param name="metrics">Performance metrics from plan analysis</param>
    /// <returns>List of optimization suggestions</returns>
    protected List<string> GenerateOptimizationTips(PerformanceMetrics metrics)
    {
        var tips = new List<string>();
        
        if (metrics.HasTableScan)
        {
            tips.Add("Query contains full table scan - consider adding indexes");
        }
        
        if (metrics.HasSort && metrics.TotalRowsProduced > 10000)
        {
            tips.Add("Large sort operation detected - verify ORDER BY is necessary or add index");
        }
        
        if (metrics.HasIneffectiveNestedLoop)
        {
            tips.Add("Nested loop join with many rows - consider reordering joins or adding indexes");
        }
        
        if (metrics.Selectivity < 0.01)
        {
            tips.Add("Low selectivity - query examines many rows to return few results");
        }
        
        if (metrics.NestedLoopJoinCount > 2)
        {
            tips.Add("Multiple nested loop joins - consider using hash or merge join");
        }
        
        return tips;
    }
}
