using Dialect.Core.QueryRewrite;
using QueryRewriteRecord = Dialect.Core.QueryRewrite.QueryRewrite;
using QueryRewriteBase = Dialect.Core.QueryRewrite.QueryRewriteAdvisor;

namespace Dialect.SqlServer.QueryRewrite;

/// <summary>
/// SQL Server-specific query rewrite advisor
/// </summary>
public class SqlServerRewriteAdvisor : QueryRewriteBase
{
    public override IReadOnlyList<global::Dialect.Core.QueryRewrite.QueryRewrite> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var rewrites = new List<global::Dialect.Core.QueryRewrite.QueryRewrite>();

        // CTE for multiple subquery usage
        if (CanBenefitFromCte(queryText))
        {
            var cteRewrite = new QueryRewriteRecord(
                RewriteId: "SQLSERVER_CTE_SUBQUERY",
                Category: "CTE",
                Title: "Convert subqueries to CTE",
                Description: "Multiple nested subqueries can be optimized using Common Table Expressions (CTE)",
                OriginalPattern: "SELECT * FROM (SELECT ...) sub1 WHERE id IN (SELECT ...)",
                SuggestedPattern: "WITH cte AS (SELECT ...) SELECT * FROM cte WHERE id IN (...)",
                ImprovementPercentage: 15,
                ImplementationComplexity: CalculateComplexity("CTE"),
                Priority: 4,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(15, 2),
                AffectedComponents: new List<string> { "SELECT", "WHERE" },
                ApplicabilityConditions: new List<string> { "SQL Server 2005+" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>()
            );

            rewrites.Add(cteRewrite);
        }

        // Window functions for aggregation
        if (DetectAggregationIssue(queryText))
        {
            var windowRewrite = new QueryRewriteRecord(
                RewriteId: "SQLSERVER_WINDOW_FUNCTION",
                Category: "WindowFunction",
                Title: "Use window functions for analytics",
                Description: "Window functions can improve performance of aggregation queries",
                OriginalPattern: "SELECT col1, COUNT(*) OVER (PARTITION BY col1)",
                SuggestedPattern: "SELECT col1, COUNT(*) OVER (PARTITION BY col1) AS cnt FROM table",
                ImprovementPercentage: 20,
                ImplementationComplexity: CalculateComplexity("WindowFunction"),
                Priority: 3,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(20, 3),
                AffectedComponents: new List<string> { "SELECT", "GROUP BY" },
                ApplicabilityConditions: new List<string> { "SQL Server 2012+" },
                Tradeoffs: new List<string> { "Query logic may change significantly" },
                DialectSpecificNotes: new Dictionary<string, string>()
            );

            rewrites.Add(windowRewrite);
        }

        // Join order optimization
        if (DetectInefficientJoin(queryText))
        {
            var joinRewrite = new QueryRewriteRecord(
                RewriteId: "SQLSERVER_JOIN_ORDER",
                Category: "JoinOrder",
                Title: "Optimize join order",
                Description: "Reorder joins to place most restrictive conditions first",
                OriginalPattern: "SELECT * FROM A JOIN B JOIN C WHERE c.id = ...",
                SuggestedPattern: "SELECT * FROM C JOIN B JOIN A WHERE c.id = ... (reordered)",
                ImprovementPercentage: 25,
                ImplementationComplexity: CalculateComplexity("JoinOrder"),
                Priority: 4,
                RiskLevel: "Medium",
                RoiScore: CalculateRewriteRoiScore(25, 3),
                AffectedComponents: new List<string> { "JOIN" },
                ApplicabilityConditions: new List<string> { "3+ joins in query" },
                Tradeoffs: new List<string> { "May require index changes" },
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Hint", "Use OPTION (FORCE ORDER) to override optimizer if needed" }
                }
            );

            rewrites.Add(joinRewrite);
        }

        return rewrites;
    }
}
