using Dialect.Core.QueryRewrite;
using QueryRewriteRecord = Dialect.Core.QueryRewrite.QueryRewrite;
using QueryRewriteBase = Dialect.Core.QueryRewrite.QueryRewriteAdvisor;

namespace Dialect.MySql.QueryRewrite;

/// <summary>
/// MySQL-specific query rewrite advisor
/// </summary>
public class MySqlRewriteAdvisor : QueryRewriteBase
{
    public override IReadOnlyList<global::Dialect.Core.QueryRewrite.QueryRewrite> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var rewrites = new List<global::Dialect.Core.QueryRewrite.QueryRewrite>();
        
        // CTE (MySQL 8.0+)
        if (CanBenefitFromCte(queryText))
        {
            var cteRewrite = new QueryRewriteRecord(
                RewriteId: "MYSQL_CTE",
                Category: "CTE",
                Title: "Use CTE for recursive or repeated subqueries",
                Description: "Common Table Expressions can improve readability and performance",
                OriginalPattern: "SELECT * FROM (SELECT ...) AS sub WHERE ...",
                SuggestedPattern: "WITH cte AS (SELECT ...) SELECT * FROM cte WHERE ...",
                ImprovementPercentage: 12,
                ImplementationComplexity: CalculateComplexity("CTE"),
                Priority: 3,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(12, 2),
                AffectedComponents: new List<string> { "SELECT", "WHERE" },
                ApplicabilityConditions: new List<string> { "MySQL 8.0+" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>()
            );
            
            rewrites.Add(cteRewrite);
        }
        
        // IN to JOIN conversion
        if (queryText.Contains("IN (SELECT", StringComparison.OrdinalIgnoreCase))
        {
            var joinRewrite = new QueryRewriteRecord(
                RewriteId: "MYSQL_IN_TO_JOIN",
                Category: "QueryRewrite",
                Title: "Convert IN subquery to JOIN",
                Description: "JOINs can be more efficient than IN subqueries in MySQL",
                OriginalPattern: "SELECT * FROM orders WHERE customer_id IN (SELECT id FROM customers WHERE status = 'active')",
                SuggestedPattern: "SELECT DISTINCT o.* FROM orders o JOIN customers c ON o.customer_id = c.id WHERE c.status = 'active'",
                ImprovementPercentage: 22,
                ImplementationComplexity: CalculateComplexity("Subquery"),
                Priority: 4,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(22, 2),
                AffectedComponents: new List<string> { "WHERE" },
                ApplicabilityConditions: new List<string> { "All MySQL versions" },
                Tradeoffs: new List<string> { "DISTINCT may add overhead" },
                DialectSpecificNotes: new Dictionary<string, string>()
            );
            
            rewrites.Add(joinRewrite);
        }
        
        // Window function for ranking
        if (queryText.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
        {
            var windowRewrite = new QueryRewriteRecord(
                RewriteId: "MYSQL_ROW_NUMBER",
                Category: "WindowFunction",
                Title: "Use ROW_NUMBER() for pagination",
                Description: "ROW_NUMBER window function is cleaner and faster for pagination",
                OriginalPattern: "SELECT * FROM (SELECT * FROM orders ORDER BY id LIMIT 10) AS page1",
                SuggestedPattern: "SELECT * FROM orders WHERE ROW_NUMBER() OVER (ORDER BY id) BETWEEN 1 AND 10",
                ImprovementPercentage: 18,
                ImplementationComplexity: CalculateComplexity("WindowFunction"),
                Priority: 3,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(18, 3),
                AffectedComponents: new List<string> { "ORDER BY", "LIMIT" },
                ApplicabilityConditions: new List<string> { "MySQL 8.0+" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>()
            );
            
            rewrites.Add(windowRewrite);
        }
        
        // Batch insert optimization
        if (queryText.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            var batchRewrite = new QueryRewriteRecord(
                RewriteId: "MYSQL_BATCH_INSERT",
                Category: "QueryRewrite",
                Title: "Use batch INSERT for multiple rows",
                Description: "Single INSERT with multiple VALUES is faster than repeated INSERTs",
                OriginalPattern: "INSERT INTO table VALUES (...); INSERT INTO table VALUES (...);",
                SuggestedPattern: "INSERT INTO table VALUES (...), (...), (...);",
                ImprovementPercentage: 40,
                ImplementationComplexity: CalculateComplexity("CTE"),
                Priority: 4,
                RiskLevel: "Low",
                RoiScore: CalculateRewriteRoiScore(40, 2),
                AffectedComponents: new List<string> { "INSERT" },
                ApplicabilityConditions: new List<string> { "All MySQL versions" },
                Tradeoffs: new List<string>(),
                DialectSpecificNotes: new Dictionary<string, string>
                {
                    { "Note", "Check max_allowed_packet setting for large batches" }
                }
            );
            
            rewrites.Add(batchRewrite);
        }
        
        return rewrites;
    }
}
