using Dialect.Core.QueryRewrite;
using QueryRewriteRecord = Dialect.Core.QueryRewrite.QueryRewrite;
using QueryRewriteBase = Dialect.Core.QueryRewrite.QueryRewriteAdvisor;

namespace Dialect.PostgreSql.QueryRewrite;

/// <summary>
/// PostgreSQL-specific query rewrite advisor
/// </summary>
public class PostgreSqlRewriteAdvisor : QueryRewriteBase
{
    public override IReadOnlyList<global::Dialect.Core.QueryRewrite.QueryRewrite> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var rewrites = new List<global::Dialect.Core.QueryRewrite.QueryRewrite>();
        
        // Materialized view for complex aggregation
        if (DetectAggregationIssue(queryText) && DetectInefficientJoin(queryText))
        {
            var matviewRewrite = new QueryRewriteRecord(
                RewriteId: "POSTGRES_MATERIALIZED_VIEW",
                Category: "Materialization",
                Title: "Create materialized view",
                Description: "Complex aggregation queries can benefit from materialization",
                OriginalPattern: "SELECT complex aggregation FROM multiple joins...",
                SuggestedPattern: "CREATE MATERIALIZED VIEW mv_name AS SELECT ...; CREATE INDEX ON mv_name(...);",
                ImprovementPercentage: 50,
                ImplementationComplexity: CalculateComplexity("Materialization"),
                Priority: 3,
                RiskLevel: "High",
                RoiScore: CalculateRewriteRoiScore(50, 4),
                AffectedComponents: new List<string> { "SELECT", "FROM", "JOIN" },
                ApplicabilityConditions: new List<string> { "Query used frequently", "Acceptable staleness" },
                Tradeoffs: new List<string> { "Requires refresh strategy", "Additional storage needed" },
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Refresh", "Consider REFRESH MATERIALIZED VIEW CONCURRENTLY" }
                }
            );
            
            rewrites.Add(matviewRewrite);
        }
        
        // UNION optimization
        if (DetectUnionOptimization(queryText))
        {
            var unionRewrite = new QueryRewriteRecord(
                RewriteId: "POSTGRES_UNION_ALL",
                Category: "UNION",
                Title: "Use UNION ALL instead of UNION",
                Description: "If duplicates are acceptable, UNION ALL avoids deduplication overhead",
                OriginalPattern: "SELECT ... UNION SELECT ...",
                SuggestedPattern: "SELECT ... UNION ALL SELECT ... (if no duplicates expected)",
                ImprovementPercentage: 30,
                ImplementationComplexity: CalculateComplexity("UNION"),
                Priority: 4,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(30, 2),
                AffectedComponents: new List<string> { "UNION" },
                ApplicabilityConditions: new List<string> { "Duplicates not expected" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>()
            );
            
            rewrites.Add(unionRewrite);
        }
        
        // Window functions with partitioning
        if (DetectAggregationIssue(queryText))
        {
            var windowRewrite = new QueryRewriteRecord(
                RewriteId: "POSTGRES_WINDOW_FUNCTION",
                Category: "WindowFunction",
                Title: "Use window functions for ranking/analytics",
                Description: "Window functions are efficient for ranking, running totals, analytics",
                OriginalPattern: "SELECT id, ROW_NUMBER() OVER (ORDER BY created_at)",
                SuggestedPattern: "SELECT id, ROW_NUMBER() OVER (PARTITION BY category ORDER BY created_at)",
                ImprovementPercentage: 18,
                ImplementationComplexity: CalculateComplexity("WindowFunction"),
                Priority: 3,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(18, 3),
                AffectedComponents: new List<string> { "SELECT", "ORDER BY" },
                ApplicabilityConditions: new List<string> { "PostgreSQL 8.4+" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>()
            );
            
            rewrites.Add(windowRewrite);
        }
        
        return rewrites;
    }
}
