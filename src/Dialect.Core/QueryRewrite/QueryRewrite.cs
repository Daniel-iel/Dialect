namespace Dialect.Core.QueryRewrite;

/// <summary>
/// Represents a suggested query rewrite opportunity
/// </summary>
public record QueryRewrite(
    /// <summary>
    /// Unique identifier for this rewrite suggestion
    /// </summary>
    string RewriteId,
    
    /// <summary>
    /// Category: CTE, WindowFunction, JoinOrder, Subquery, UNION, etc.
    /// </summary>
    string Category,
    
    /// <summary>
    /// Human-readable title of the rewrite
    /// </summary>
    string Title,
    
    /// <summary>
    /// Detailed description of why this rewrite helps
    /// </summary>
    string Description,
    
    /// <summary>
    /// Original query pattern that could be improved
    /// </summary>
    string OriginalPattern,
    
    /// <summary>
    /// Suggested rewritten query pattern
    /// </summary>
    string SuggestedPattern,
    
    /// <summary>
    /// Estimated improvement percentage (0-100)
    /// </summary>
    decimal ImprovementPercentage,
    
    /// <summary>
    /// Complexity of implementation (1-5, where 1 = simple, 5 = complex)
    /// </summary>
    int ImplementationComplexity,
    
    /// <summary>
    /// Priority for implementation (1-5, where 5 = highest)
    /// </summary>
    int Priority,
    
    /// <summary>
    /// Risk level: Low, Medium, High
    /// </summary>
    string RiskLevel,
    
    /// <summary>
    /// ROI score (improvement / complexity)
    /// </summary>
    decimal RoiScore,
    
    /// <summary>
    /// Affected query components (WHERE, JOIN, SELECT, etc.)
    /// </summary>
    IReadOnlyList<string> AffectedComponents,
    
    /// <summary>
    /// Applicability conditions (ex: "Only if rows < 1000", "PostgreSQL 13+")
    /// </summary>
    IReadOnlyList<string> ApplicabilityConditions,
    
    /// <summary>
    /// Potential drawbacks or trade-offs
    /// </summary>
    IReadOnlyList<string> Tradeoffs,
    
    /// <summary>
    /// Dialect-specific implementation notes
    /// </summary>
    IReadOnlyDictionary<string, string> DialectSpecificNotes
)
{
    /// <summary>
    /// Validates query rewrite data
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RewriteId) &&
        !string.IsNullOrWhiteSpace(Category) &&
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(OriginalPattern) &&
        !string.IsNullOrWhiteSpace(SuggestedPattern) &&
        ImprovementPercentage >= 0 && ImprovementPercentage <= 100 &&
        ImplementationComplexity >= 1 && ImplementationComplexity <= 5 &&
        Priority >= 1 && Priority <= 5 &&
        !string.IsNullOrWhiteSpace(RiskLevel) &&
        RoiScore >= 0;
}
