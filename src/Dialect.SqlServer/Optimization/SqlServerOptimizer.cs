using Dialect.Core.Performance;
using Dialect.Core.Optimization;

namespace Dialect.SqlServer.Optimization;

/// <summary>
/// SQL Server-specific optimization recommendation engine
/// </summary>
public class SqlServerOptimizer : OptimizationEngine
{
    public override IReadOnlyList<OptimizationRecommendation> GenerateRecommendations(
        string queryText,
        PerformanceMetrics metrics,
        QueryExecutionPlan executionPlan,
        IDictionary<string, object>? contextData = null)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        // Index recommendations
        recommendations.AddRange(GenerateIndexRecommendations(metrics, executionPlan));
        
        // Join recommendations
        recommendations.AddRange(GenerateJoinRecommendations(metrics, executionPlan));
        
        // Sort recommendations
        recommendations.AddRange(GenerateSortRecommendations(metrics, executionPlan));
        
        // Selectivity recommendations
        recommendations.AddRange(GenerateSelectivityRecommendations(metrics, executionPlan));
        
        // SQL Server-specific recommendations
        recommendations.AddRange(GenerateSqlServerSpecificRecommendations(queryText, metrics, executionPlan));
        
        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }
    
    /// <summary>
    /// Generates SQL Server-specific recommendations
    /// </summary>
    private List<OptimizationRecommendation> GenerateSqlServerSpecificRecommendations(
        string queryText,
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();
        
        // Parallelism recommendation for large queries
        if (metrics.TotalCost > 50 && metrics.ExecutionTimeMs > 500)
        {
            var parallelRec = new OptimizationRecommendation(
                RecommendationId: "SQLSERVER_PARALLELISM",
                Category: "ExecutionPlan",
                Title: "Enable query parallelism",
                Description: "High-cost query may benefit from parallel execution. SQL Server can use multiple CPU cores.",
                SqlStatement: "SET MAXDOP 0;  -- Allow SQL Server to determine optimal parallelism",
                ImprovementPercentage: 25,
                ImplementationCost: 5,
                Priority: 3,
                RoiScore: 5.0m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Note", "Test parallelism impact; some queries perform better with MAXDOP 1" }
                },
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 5,
                References: new List<string> { "MAXDOP documentation", "Query parallelism tuning" }
            );
            recommendations.Add(parallelRec);
        }
        
        // Statistics update recommendation
        if (metrics.Selectivity < 0.05)
        {
            var statsRec = new OptimizationRecommendation(
                RecommendationId: "SQLSERVER_UPDATE_STATS",
                Category: "Maintenance",
                Title: "Update table statistics",
                Description: "Outdated statistics may cause optimizer to make suboptimal join order decisions.",
                SqlStatement: "UPDATE STATISTICS [TableName] WITH FULLSCAN;",
                ImprovementPercentage: 15,
                ImplementationCost: 10,
                Priority: 2,
                RoiScore: 1.5m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 10,
                References: new List<string> { "UPDATE STATISTICS", "Query optimizer improvements" }
            );
            recommendations.Add(statsRec);
        }
        
        // Columnstore index recommendation for large tables
        if (metrics.TotalRowsExamined > 1000000 && metrics.TableScanCount > 0)
        {
            var csRec = new OptimizationRecommendation(
                RecommendationId: "SQLSERVER_COLUMNSTORE",
                Category: "Index",
                Title: "Consider clustered columnstore index",
                Description: "Large table with full scans could benefit from columnstore compression and parallel processing.",
                SqlStatement: "CREATE CLUSTERED COLUMNSTORE INDEX [CCI_TableName] ON [TableName];",
                ImprovementPercentage: 40,
                ImplementationCost: 60,
                Priority: 2,
                RoiScore: 0.67m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Warning", "Columnstore indexes have limitations on UPDATE/DELETE operations" }
                },
                RiskLevel: "High",
                EstimatedImplementationTimeMinutes: 60,
                References: new List<string> { "Columnstore indexes documentation" }
            );
            recommendations.Add(csRec);
        }
        
        // Table variable vs temp table recommendation
        if (queryText.Contains("DECLARE @", StringComparison.OrdinalIgnoreCase))
        {
            var varRec = new OptimizationRecommendation(
                RecommendationId: "SQLSERVER_TEMP_TABLE",
                Category: "QueryRewrite",
                Title: "Review table variable usage",
                Description: "Table variables have no statistics and may cause performance issues with larger datasets.",
                SqlStatement: null,
                ImprovementPercentage: 20,
                ImplementationCost: 30,
                Priority: 2,
                RoiScore: 0.67m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Medium",
                EstimatedImplementationTimeMinutes: 20,
                References: new List<string> { "Table variables vs temp tables", "Performance tips" }
            );
            recommendations.Add(varRec);
        }
        
        return recommendations;
    }
}
