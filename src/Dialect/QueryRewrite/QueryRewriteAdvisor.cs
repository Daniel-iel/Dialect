namespace Dialect.Core.QueryRewrite;

/// <summary>
/// Base class for query rewrite advisors
/// Suggests query optimization rewrites
/// </summary>
public abstract class QueryRewriteAdvisor
{
    /// <summary>
    /// Analyzes query and generates rewrite suggestions
    /// </summary>
    public abstract IReadOnlyList<QueryRewrite> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null);
    
    /// <summary>
    /// Protected helper: Validate rewrite suggestion
    /// </summary>
    protected bool ValidateRewrite(QueryRewrite rewrite)
    {
        return rewrite.IsValid &&
               !string.IsNullOrWhiteSpace(rewrite.OriginalPattern) &&
               !string.IsNullOrWhiteSpace(rewrite.SuggestedPattern);
    }
    
    /// <summary>
    /// Protected helper: Calculate ROI score for rewrite
    /// </summary>
    protected decimal CalculateRewriteRoiScore(
        decimal improvement,
        int complexity)
    {
        if (complexity == 0) return 0;
        return Math.Round(improvement / complexity, 2);
    }
    
    /// <summary>
    /// Protected helper: Detect subquery patterns
    /// </summary>
    protected bool DetectSubqueries(string queryText)
    {
        return queryText.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
               (queryText.Contains("(SELECT", StringComparison.OrdinalIgnoreCase) ||
                queryText.Contains("FROM (", StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Protected helper: Detect CTE candidates
    /// </summary>
    protected bool CanBenefitFromCte(string queryText)
    {
        // CTE beneficial for multiple subquery usage or complex nested queries
        var subqueryCount = System.Text.RegularExpressions.Regex.Matches(
            queryText, @"\(SELECT", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        
        return subqueryCount >= 2;
    }
    
    /// <summary>
    /// Protected helper: Detect inefficient nested loop patterns
    /// </summary>
    protected bool DetectInefficientJoin(string queryText)
    {
        // Pattern: multiple joins without explicit index hints
        var joinCount = System.Text.RegularExpressions.Regex.Matches(
            queryText, @"JOIN\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        
        // Also check for n+1 pattern (multiple WHERE IN with subqueries)
        var whereInCount = System.Text.RegularExpressions.Regex.Matches(
            queryText, @"WHERE.*IN\s*\(SELECT", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        
        return joinCount >= 3 || whereInCount >= 2;
    }
    
    /// <summary>
    /// Protected helper: Detect aggregation without proper grouping
    /// </summary>
    protected bool DetectAggregationIssue(string queryText)
    {
        var hasAggregate = System.Text.RegularExpressions.Regex.IsMatch(
            queryText, @"COUNT|SUM|AVG|MAX|MIN|GROUP_CONCAT|STRING_AGG",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        var hasGroupBy = queryText.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase);
        
        // Aggregate without GROUP BY might benefit from CTE or window function
        return hasAggregate;
    }
    
    /// <summary>
    /// Protected helper: Detect UNION optimization opportunities
    /// </summary>
    protected bool DetectUnionOptimization(string queryText)
    {
        var unionCount = System.Text.RegularExpressions.Regex.Matches(
            queryText, @"UNION", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        
        return unionCount >= 2;
    }
    
    /// <summary>
    /// Protected helper: Calculate complexity score
    /// </summary>
    protected int CalculateComplexity(string category)
    {
        return category switch
        {
            "CTE" => 2,                     // Low complexity
            "WindowFunction" => 3,          // Medium complexity
            "JoinOrder" => 3,               // Medium complexity
            "Subquery" => 2,                // Low complexity
            "UNION" => 2,                   // Low complexity
            "Materialization" => 4,         // High complexity
            _ => 3                          // Default medium
        };
    }
    
    /// <summary>
    /// Protected helper: Extract affected query components
    /// </summary>
    protected List<string> ExtractAffectedComponents(string queryText)
    {
        var components = new List<string>();
        
        if (queryText.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
            components.Add("WHERE");
        if (queryText.Contains("JOIN", StringComparison.OrdinalIgnoreCase))
            components.Add("JOIN");
        if (queryText.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            components.Add("SELECT");
        if (queryText.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
            components.Add("ORDER BY");
        if (queryText.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase))
            components.Add("GROUP BY");
        if (queryText.Contains("HAVING", StringComparison.OrdinalIgnoreCase))
            components.Add("HAVING");
        
        return components;
    }
}
