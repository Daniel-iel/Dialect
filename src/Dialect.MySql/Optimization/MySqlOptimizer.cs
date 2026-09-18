using Dialect.Core.Performance;
using Dialect.Core.Optimization;

namespace Dialect.MySql.Optimization;

/// <summary>
/// MySQL-specific optimization recommendation engine
/// </summary>
public class MySqlOptimizer : OptimizationEngine
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

        // MySQL-specific recommendations
        recommendations.AddRange(GenerateMySqlSpecificRecommendations(queryText, metrics, executionPlan));

        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }

    /// <summary>
    /// Generates MySQL-specific recommendations
    /// </summary>
    private List<OptimizationRecommendation> GenerateMySqlSpecificRecommendations(
        string queryText,
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();

        // ANALYZE TABLE recommendation
        if (metrics.Selectivity < 0.05)
        {
            var analyzeRec = new OptimizationRecommendation(
                RecommendationId: "MYSQL_ANALYZE_TABLE",
                Category: "Maintenance",
                Title: "Run ANALYZE TABLE to update statistics",
                Description: "MySQL optimizer needs current statistics. Low selectivity suggests outdated key distribution statistics.",
                SqlStatement: "ANALYZE TABLE [table_name];",
                ImprovementPercentage: 18,
                ImplementationCost: 8,
                Priority: 4,
                RoiScore: 2.25m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 5,
                References: new List<string> { "ANALYZE TABLE documentation", "Statistics management" }
            );
            recommendations.Add(analyzeRec);
        }

        // Composite index recommendation
        if (metrics.FilterOperationCount > 0 && metrics.TableScanCount > 0)
        {
            var compositeRec = new OptimizationRecommendation(
                RecommendationId: "MYSQL_COMPOSITE_INDEX",
                Category: "Index",
                Title: "Create composite index for filter conditions",
                Description: "Multiple filter conditions in WHERE clause can benefit from composite index matching column order.",
                SqlStatement: "CREATE INDEX idx_composite ON [table_name] ([col1], [col2], [col3]);",
                ImprovementPercentage: 30,
                ImplementationCost: 18,
                Priority: 4,
                RoiScore: 1.67m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Tip", "Column order matters: equality conditions first, then range/sort conditions" }
                },
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 12,
                References: new List<string> { "Composite indexes", "Index design" }
            );
            recommendations.Add(compositeRec);
        }

        // Generated column index recommendation
        if (queryText.Contains("FUNCTION", StringComparison.OrdinalIgnoreCase) && metrics.TableScanCount > 0)
        {
            var genColRec = new OptimizationRecommendation(
                RecommendationId: "MYSQL_GENERATED_COLUMN",
                Category: "Index",
                Title: "Use generated column with index for function-based filtering",
                Description: "MySQL 5.7+ supports generated columns. Pre-computing function results avoids repeated calculations.",
                SqlStatement: "ALTER TABLE [table_name] ADD COLUMN [col_gen] [type] GENERATED ALWAYS AS ([function]) STORED;",
                ImprovementPercentage: 35,
                ImplementationCost: 50,
                Priority: 2,
                RoiScore: 0.7m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Version", "Requires MySQL 5.7.6+" }
                },
                RiskLevel: "Medium",
                EstimatedImplementationTimeMinutes: 40,
                References: new List<string> { "Generated columns", "Virtual and stored columns" }
            );
            recommendations.Add(genColRec);
        }

        // Range partition recommendation for large tables
        if (metrics.TotalRowsExamined > 5000000)
        {
            var partitionRec = new OptimizationRecommendation(
                RecommendationId: "MYSQL_PARTITIONING",
                Category: "QueryRewrite",
                Title: "Consider table partitioning for large table",
                Description: "Very large table could benefit from range or key partitioning to improve query pruning.",
                SqlStatement: "ALTER TABLE [table_name] PARTITION BY RANGE (YEAR([date_column])) (PARTITION p0 VALUES LESS THAN (2020), ...);",
                ImprovementPercentage: 40,
                ImplementationCost: 70,
                Priority: 2,
                RoiScore: 0.57m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Complexity", "High complexity; requires careful planning" }
                },
                RiskLevel: "High",
                EstimatedImplementationTimeMinutes: 120,
                References: new List<string> { "Table partitioning", "Partition management" }
            );
            recommendations.Add(partitionRec);
        }

        // Query cache hint recommendation (if query is eligible)
        if (metrics.ExecutionTimeMs > 500 && !queryText.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            var cacheRec = new OptimizationRecommendation(
                RecommendationId: "MYSQL_QUERY_CACHE",
                Category: "ExecutionPlan",
                Title: "Cache-friendly query optimization",
                Description: "Slow SELECT query may benefit from query caching if it's repeated frequently with same parameters.",
                SqlStatement: "SELECT SQL_CACHE ... FROM ... ;  -- Enable query cache for this query",
                ImprovementPercentage: 95,
                ImplementationCost: 5,
                Priority: 2,
                RoiScore: 19.0m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Note", "Query cache removed in MySQL 8.0+" }
                },
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 2,
                References: new List<string> { "Query cache", "MySQL 8.0 changes" }
            );
            recommendations.Add(cacheRec);
        }

        return recommendations;
    }
}
