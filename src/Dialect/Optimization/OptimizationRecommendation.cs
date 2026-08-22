namespace Dialect.Core.Optimization;

/// <summary>
/// Represents a single optimization recommendation based on query analysis.
/// </summary>
public record OptimizationRecommendation(
    /// <summary>
    /// Unique identifier for the recommendation type
    /// </summary>
    string RecommendationId,
    
    /// <summary>
    /// Human-readable category (e.g., "Index", "QueryRewrite", "JoinOrder", "Partitioning")
    /// </summary>
    string Category,
    
    /// <summary>
    /// Concise title of the recommendation (e.g., "Add index on Orders.CustomerId")
    /// </summary>
    string Title,
    
    /// <summary>
    /// Detailed description explaining why this optimization helps
    /// </summary>
    string Description,
    
    /// <summary>
    /// SQL statement or hint to implement (nullable for non-SQL recommendations)
    /// </summary>
    string? SqlStatement,
    
    /// <summary>
    /// Estimated performance improvement (0-100, percentage)
    /// </summary>
    decimal ImprovementPercentage,
    
    /// <summary>
    /// Estimated implementation cost (0-100, where 100 = very expensive)
    /// </summary>
    int ImplementationCost,
    
    /// <summary>
    /// Priority ranking (1-5, where 5 = highest priority)
    /// </summary>
    int Priority,
    
    /// <summary>
    /// Return on investment score (calculated from improvement/cost ratio)
    /// </summary>
    decimal RoiScore,
    
    /// <summary>
    /// Affected query/table/index names
    /// </summary>
    IReadOnlyList<string> AffectedObjects,
    
    /// <summary>
    /// Dialect-specific implementation notes
    /// </summary>
    IReadOnlyDictionary<string, string> DialectSpecificNotes,
    
    /// <summary>
    /// Risk level: Low, Medium, High
    /// </summary>
    string RiskLevel,
    
    /// <summary>
    /// Approximate estimated time to implement (in minutes)
    /// </summary>
    int EstimatedImplementationTimeMinutes,
    
    /// <summary>
    /// Reference links or additional resources
    /// </summary>
    IReadOnlyList<string> References
)
{
    /// <summary>
    /// Validates recommendation data consistency
    /// </summary>
    public bool IsValid => 
        !string.IsNullOrWhiteSpace(RecommendationId) &&
        !string.IsNullOrWhiteSpace(Category) &&
        !string.IsNullOrWhiteSpace(Title) &&
        ImprovementPercentage >= 0 && ImprovementPercentage <= 100 &&
        ImplementationCost >= 0 && ImplementationCost <= 100 &&
        Priority >= 1 && Priority <= 5 &&
        RoiScore >= 0 &&
        !string.IsNullOrWhiteSpace(RiskLevel) &&
        EstimatedImplementationTimeMinutes >= 0;
}
