using Dialect.Core.Performance;

namespace Dialect.Core.Optimization;

/// <summary>
/// Base orchestrator for generating optimization recommendations.
/// Synthesizes query analysis, performance metrics, and execution plans.
/// </summary>
public abstract class OptimizationEngine
{
    /// <summary>
    /// Generates optimization recommendations based on query performance analysis
    /// </summary>
    public abstract IReadOnlyList<OptimizationRecommendation> GenerateRecommendations(
        string queryText,
        PerformanceMetrics metrics,
        QueryExecutionPlan executionPlan,
        IDictionary<string, object>? contextData = null);
    
    /// <summary>
    /// Protected helper: Generate index-related recommendations
    /// </summary>
    protected List<OptimizationRecommendation> GenerateIndexRecommendations(
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        // Recommend indexes for table scans
        if (metrics.HasTableScan && metrics.TableScanCount > 0)
        {
            var tableName = ExtractTableNameFromPlan(plan);
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                var indexRec = new OptimizationRecommendation(
                    RecommendationId: $"INDEX_SCAN_{tableName}",
                    Category: "Index",
                    Title: $"Add index to avoid table scan on {tableName}",
                    Description: $"Table scan detected on {tableName}. Adding an index on filter columns can reduce cost.",
                    SqlStatement: $"CREATE INDEX idx_{tableName}_optimized ON {tableName} (...)",
                    ImprovementPercentage: CalculateIndexImprovementPercentage(metrics),
                    ImplementationCost: 15,
                    Priority: 4,
                    RoiScore: CalculateIndexRoiScore(metrics),
                    AffectedObjects: new[] { tableName },
                    DialectSpecificNotes: new Dictionary<string, string>(),
                    RiskLevel: "Low",
                    EstimatedImplementationTimeMinutes: 10,
                    References: new List<string> { "SQL Server: CREATE INDEX docs", "PostgreSQL: CREATE INDEX docs" }
                );
                recommendations.Add(indexRec);
            }
        }
        
        // Recommend covering indexes for frequent accesses
        if (metrics.IndexSeekCount > 0 && metrics.TotalRowsProduced > 10000)
        {
            var coveringRec = new OptimizationRecommendation(
                RecommendationId: "INDEX_COVERING",
                Category: "Index",
                Title: "Consider covering index for SELECT columns",
                Description: "A covering index can eliminate key lookups by including additional columns.",
                SqlStatement: null,
                ImprovementPercentage: 15,
                ImplementationCost: 20,
                Priority: 3,
                RoiScore: CalculateCoveringIndexRoiScore(metrics),
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 15,
                References: new List<string> { "Covering Indexes tutorial" }
            );
            recommendations.Add(coveringRec);
        }
        
        return recommendations;
    }
    
    /// <summary>
    /// Protected helper: Generate join-related recommendations
    /// </summary>
    protected List<OptimizationRecommendation> GenerateJoinRecommendations(
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        // Recommend join order optimization
        if (metrics.NestedLoopJoinCount > 1)
        {
            var joinRec = new OptimizationRecommendation(
                RecommendationId: "JOIN_ORDER_OPTIMIZATION",
                Category: "QueryRewrite",
                Title: "Consider join order optimization",
                Description: "Multiple nested loop joins detected. Reordering joins or using hash joins may improve performance.",
                SqlStatement: null,
                ImprovementPercentage: CalculateJoinOptimizationImprovement(metrics),
                ImplementationCost: 30,
                Priority: 3,
                RoiScore: CalculateJoinOptimizationRoi(metrics),
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Medium",
                EstimatedImplementationTimeMinutes: 30,
                References: new List<string> { "Query Optimizer documentation" }
            );
            recommendations.Add(joinRec);
        }
        
        // Recommend replacing nested loop with hash join
        if (metrics.HasIneffectiveNestedLoop)
        {
            var hashJoinRec = new OptimizationRecommendation(
                RecommendationId: "HASH_JOIN_RECOMMENDATION",
                Category: "QueryRewrite",
                Title: "Use hash join instead of nested loop",
                Description: "Current nested loop join is inefficient for this data distribution. Hash join may be faster.",
                SqlStatement: null,
                ImprovementPercentage: 25,
                ImplementationCost: 25,
                Priority: 4,
                RoiScore: 1.0m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 5,
                References: new List<string> { "Join algorithm comparison" }
            );
            recommendations.Add(hashJoinRec);
        }
        
        return recommendations;
    }
    
    /// <summary>
    /// Protected helper: Generate sort-related recommendations
    /// </summary>
    protected List<OptimizationRecommendation> GenerateSortRecommendations(
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        if (metrics.HasSort && metrics.SortOperationCount > 0)
        {
            // Expensive sort detected
            if (metrics.TotalRowsProduced > 50000)
            {
                var sortRec = new OptimizationRecommendation(
                    RecommendationId: "EXPENSIVE_SORT",
                    Category: "QueryRewrite",
                    Title: "Reduce rows before expensive sort",
                    Description: $"Sorting {metrics.TotalRowsProduced} rows is expensive. Apply filters earlier in query.",
                    SqlStatement: null,
                    ImprovementPercentage: 20,
                    ImplementationCost: 35,
                    Priority: 3,
                    RoiScore: CalculateSortRoiScore(metrics),
                    AffectedObjects: ExtractTableNamesFromPlan(plan),
                    DialectSpecificNotes: new Dictionary<string, string>(),
                    RiskLevel: "Medium",
                    EstimatedImplementationTimeMinutes: 20,
                    References: new List<string> { "Query optimization techniques" }
                );
                recommendations.Add(sortRec);
            }
        }
        
        return recommendations;
    }
    
    /// <summary>
    /// Protected helper: Generate selectivity-based recommendations
    /// </summary>
    protected List<OptimizationRecommendation> GenerateSelectivityRecommendations(
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        // Low selectivity warning
        if (metrics.Selectivity < 0.1 && metrics.TotalRowsExamined > 10000)
        {
            var selectivityRec = new OptimizationRecommendation(
                RecommendationId: "LOW_SELECTIVITY",
                Category: "QueryRewrite",
                Title: "Improve query selectivity",
                Description: $"Only {metrics.Selectivity:P} of examined rows are returned. Consider adding more specific filters.",
                SqlStatement: null,
                ImprovementPercentage: CalculateSelectivityImprovement(metrics),
                ImplementationCost: 40,
                Priority: 2,
                RoiScore: CalculateSelectivityRoi(metrics),
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 15,
                References: new List<string> { "Query predicates documentation" }
            );
            recommendations.Add(selectivityRec);
        }
        
        return recommendations;
    }
    
    // Protected helper methods for calculations and extraction
    
    protected virtual string ExtractTableNameFromPlan(QueryExecutionPlan plan)
    {
        return ExtractTableNameFromNode(plan.RootNode) ?? "Unknown";
    }
    
    private static string? ExtractTableNameFromNode(ExecutionPlanNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ObjectName))
            return node.ObjectName;
        
        foreach (var child in node.Children)
        {
            var name = ExtractTableNameFromNode(child);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        
        return null;
    }
    
    protected virtual IReadOnlyList<string> ExtractTableNamesFromPlan(QueryExecutionPlan plan)
    {
        var tables = new HashSet<string>();
        CollectTableNames(plan.RootNode, tables);
        return tables.ToList();
    }
    
    private static void CollectTableNames(ExecutionPlanNode node, HashSet<string> tables)
    {
        if (!string.IsNullOrWhiteSpace(node.ObjectName))
            tables.Add(node.ObjectName);
        
        foreach (var child in node.Children)
            CollectTableNames(child, tables);
    }
    
    protected virtual decimal CalculateIndexImprovementPercentage(PerformanceMetrics metrics)
    {
        // More rows examined = more improvement potential
        var improvement = Math.Min(60, (decimal)Math.Log10(metrics.TotalRowsExamined) * 10);
        return improvement;
    }
    
    protected virtual decimal CalculateIndexRoiScore(PerformanceMetrics metrics)
    {
        if (metrics.TotalCost == 0) return 0;
        
        var improvement = CalculateIndexImprovementPercentage(metrics);
        var implementationCost = 15;
        return Math.Round(improvement / implementationCost, 2);
    }
    
    protected virtual decimal CalculateCoveringIndexRoiScore(PerformanceMetrics metrics)
    {
        return metrics.IndexSeekCount > 0 ? 0.75m : 0;
    }
    
    protected virtual decimal CalculateJoinOptimizationImprovement(PerformanceMetrics metrics)
    {
        return Math.Min(50, metrics.NestedLoopJoinCount * 10);
    }
    
    protected virtual decimal CalculateJoinOptimizationRoi(PerformanceMetrics metrics)
    {
        var improvement = CalculateJoinOptimizationImprovement(metrics);
        return Math.Round(improvement / 30, 2);
    }
    
    protected virtual decimal CalculateSortRoiScore(PerformanceMetrics metrics)
    {
        if (metrics.TotalRowsProduced == 0) return 0;
        
        var improvement = Math.Min(40, (decimal)Math.Log10(metrics.TotalRowsProduced) * 5);
        return Math.Round(improvement / 35, 2);
    }
    
    protected virtual decimal CalculateSelectivityImprovement(PerformanceMetrics metrics)
    {
        var potentialImprovement = (1 - metrics.Selectivity) * 100;
        return Math.Min(70m, (decimal)potentialImprovement);
    }
    
    protected virtual decimal CalculateSelectivityRoi(PerformanceMetrics metrics)
    {
        var improvement = CalculateSelectivityImprovement(metrics);
        return Math.Round(improvement / 40, 2);
    }
}
