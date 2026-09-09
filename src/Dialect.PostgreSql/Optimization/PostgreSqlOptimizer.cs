using Dialect.Core.Performance;
using Dialect.Core.Optimization;

namespace Dialect.PostgreSql.Optimization;

/// <summary>
/// PostgreSQL-specific optimization recommendation engine
/// </summary>
public class PostgreSqlOptimizer : OptimizationEngine
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

        // PostgreSQL-specific recommendations
        recommendations.AddRange(GeneratePostgreSqlSpecificRecommendations(queryText, metrics, executionPlan));

        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }

    /// <summary>
    /// Generates PostgreSQL-specific recommendations
    /// </summary>
    private List<OptimizationRecommendation> GeneratePostgreSqlSpecificRecommendations(
        string queryText,
        PerformanceMetrics metrics,
        QueryExecutionPlan plan)
    {
        var recommendations = new List<OptimizationRecommendation>();

        // ANALYZE recommendation for outdated statistics
        if (metrics.Selectivity < 0.05)
        {
            var analyzeRec = new OptimizationRecommendation(
                RecommendationId: "POSTGRES_ANALYZE",
                Category: "Maintenance",
                Title: "Run ANALYZE to update table statistics",
                Description: "PostgreSQL planner needs up-to-date statistics. Very low selectivity suggests stale statistics.",
                SqlStatement: "ANALYZE [schema_name].[table_name];",
                ImprovementPercentage: 20,
                ImplementationCost: 8,
                Priority: 4,
                RoiScore: 2.5m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 5,
                References: new List<string> { "ANALYZE documentation", "Statistics collection" }
            );
            recommendations.Add(analyzeRec);
        }

        // VACUUM recommendation for bloat
        if (metrics.TotalRowsExamined > 100000)
        {
            var vacuumRec = new OptimizationRecommendation(
                RecommendationId: "POSTGRES_VACUUM",
                Category: "Maintenance",
                Title: "Consider VACUUM ANALYZE for table maintenance",
                Description: "Large table with many rows accessed. Vacuum helps reclaim space and update statistics.",
                SqlStatement: "VACUUM ANALYZE [schema_name].[table_name];",
                ImprovementPercentage: 10,
                ImplementationCost: 15,
                Priority: 2,
                RoiScore: 0.67m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>(),
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 20,
                References: new List<string> { "VACUUM documentation", "Table maintenance" }
            );
            recommendations.Add(vacuumRec);
        }

        // Partial index recommendation for filtered queries
        if (queryText.Contains("WHERE", StringComparison.OrdinalIgnoreCase) && metrics.TableScanCount > 0)
        {
            var partialIndexRec = new OptimizationRecommendation(
                RecommendationId: "POSTGRES_PARTIAL_INDEX",
                Category: "Index",
                Title: "Consider partial index for filtered queries",
                Description: "PostgreSQL supports partial indexes which reduce index size for filtered access patterns.",
                SqlStatement: "CREATE INDEX idx_partial ON [table_name] ([columns]) WHERE [condition];",
                ImprovementPercentage: 25,
                ImplementationCost: 20,
                Priority: 3,
                RoiScore: 1.25m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Benefit", "Smaller index size, faster inserts, faster index scans" }
                },
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 15,
                References: new List<string> { "Partial indexes", "Advanced indexing techniques" }
            );
            recommendations.Add(partialIndexRec);
        }

        // BRIN index recommendation for large sequential tables
        if (metrics.TotalRowsExamined > 1000000 && metrics.TableScanCount > 0)
        {
            var brinRec = new OptimizationRecommendation(
                RecommendationId: "POSTGRES_BRIN",
                Category: "Index",
                Title: "Consider BRIN index for large sequential table",
                Description: "BRIN (Block Range Index) is very compact for large tables with sequential data patterns.",
                SqlStatement: "CREATE INDEX idx_brin ON [table_name] USING BRIN ([column]);",
                ImprovementPercentage: 30,
                ImplementationCost: 10,
                Priority: 2,
                RoiScore: 3.0m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "UseCase", "Time-series data, log tables, large sequential inserts" }
                },
                RiskLevel: "Low",
                EstimatedImplementationTimeMinutes: 10,
                References: new List<string> { "BRIN indexes", "Index types in PostgreSQL" }
            );
            recommendations.Add(brinRec);
        }

        // MATERIALIZED VIEW recommendation for complex queries
        if (metrics.NestedLoopJoinCount > 2 || (metrics.TotalRowsProduced > 10000 && metrics.ExecutionTimeMs > 1000))
        {
            var matviewRec = new OptimizationRecommendation(
                RecommendationId: "POSTGRES_MATVIEW",
                Category: "QueryRewrite",
                Title: "Consider materialized view for complex query",
                Description: "Complex multi-join or aggregation query could be pre-computed in a materialized view.",
                SqlStatement: "CREATE MATERIALIZED VIEW view_name AS [query];",
                ImprovementPercentage: 50,
                ImplementationCost: 45,
                Priority: 2,
                RoiScore: 1.11m,
                AffectedObjects: ExtractTableNamesFromPlan(plan),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Tradeoff", "Faster queries but requires refresh strategy" }
                },
                RiskLevel: "Medium",
                EstimatedImplementationTimeMinutes: 45,
                References: new List<string> { "Materialized views", "View management" }
            );
            recommendations.Add(matviewRec);
        }

        return recommendations;
    }
}
