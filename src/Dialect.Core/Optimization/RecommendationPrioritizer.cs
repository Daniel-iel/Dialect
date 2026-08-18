namespace Dialect.Core.Optimization;

/// <summary>
/// Prioritizes optimization recommendations based on ROI, impact, and effort.
/// </summary>
public class RecommendationPrioritizer
{
    private readonly PrioritizationStrategy _strategy;
    
    public RecommendationPrioritizer(PrioritizationStrategy? strategy = null)
    {
        _strategy = strategy ?? PrioritizationStrategy.RoiAndPriority;
    }
    
    /// <summary>
    /// Sorts recommendations by priority score
    /// </summary>
    public IReadOnlyList<OptimizationRecommendation> Prioritize(
        IEnumerable<OptimizationRecommendation> recommendations,
        int maxRecommendations = 10)
    {
        var sorted = _strategy switch
        {
            PrioritizationStrategy.RoiOnly => 
                recommendations.OrderByDescending(r => r.RoiScore).ToList(),
            
            PrioritizationStrategy.PriorityOnly => 
                recommendations.OrderByDescending(r => r.Priority).ToList(),
            
            PrioritizationStrategy.RoiAndPriority => 
                recommendations
                    .OrderByDescending(r => CalculateCombinedScore(r))
                    .ToList(),
            
            PrioritizationStrategy.ImpactFirst => 
                recommendations
                    .OrderByDescending(r => r.ImprovementPercentage)
                    .ThenByDescending(r => r.RoiScore)
                    .ToList(),
            
            PrioritizationStrategy.LowEffortFirst => 
                recommendations
                    .OrderBy(r => r.ImplementationCost)
                    .ThenByDescending(r => r.ImprovementPercentage)
                    .ToList(),
            
            _ => recommendations.OrderByDescending(r => r.Priority).ToList()
        };
        
        return sorted.Take(maxRecommendations).ToList();
    }
    
    /// <summary>
    /// Groups recommendations by category and returns top N per category
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OptimizationRecommendation>> PrioritizeByCategory(
        IEnumerable<OptimizationRecommendation> recommendations,
        int topPerCategory = 3)
    {
        return recommendations
            .GroupBy(r => r.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OptimizationRecommendation>)g
                    .OrderByDescending(r => CalculateCombinedScore(r))
                    .Take(topPerCategory)
                    .ToList()
            );
    }
    
    /// <summary>
    /// Filters recommendations by risk threshold and minimum ROI
    /// </summary>
    public IReadOnlyList<OptimizationRecommendation> FilterByRiskAndRoi(
        IEnumerable<OptimizationRecommendation> recommendations,
        string maxRiskLevel = "Medium",
        decimal minRoiScore = 0.5m)
    {
        var riskLevels = new Dictionary<string, int>
        {
            { "Low", 1 },
            { "Medium", 2 },
            { "High", 3 }
        };
        
        var maxRiskValue = riskLevels.TryGetValue(maxRiskLevel, out var riskVal) ? riskVal : 2;
        
        return recommendations
            .Where(r => 
                (riskLevels.TryGetValue(r.RiskLevel, out var rValue) ? rValue : 3) <= maxRiskValue &&
                r.RoiScore >= minRoiScore)
            .OrderByDescending(r => CalculateCombinedScore(r))
            .ToList();
    }
    
    /// <summary>
    /// Calculates a composite score considering ROI, priority, and implementation cost
    /// </summary>
    private decimal CalculateCombinedScore(OptimizationRecommendation recommendation)
    {
        // Normalize factors to 0-1 range
        var roiScore = Math.Min(1.0m, recommendation.RoiScore / 5); // Cap at 5
        var priorityScore = recommendation.Priority / 5.0m;
        var improvementScore = recommendation.ImprovementPercentage / 100;
        var costPenalty = recommendation.ImplementationCost / 100.0m;
        
        // Weighted combination: ROI (40%), Priority (30%), Improvement (20%), Cost penalty (10%)
        var combined = 
            (roiScore * 0.4m) +
            (priorityScore * 0.3m) +
            (improvementScore * 0.2m) -
            (costPenalty * 0.1m);
        
        return Math.Round(Math.Max(0, combined), 3);
    }
}

/// <summary>
/// Strategy for prioritizing recommendations
/// </summary>
public enum PrioritizationStrategy
{
    /// <summary>
    /// Sort by ROI score alone
    /// </summary>
    RoiOnly,
    
    /// <summary>
    /// Sort by priority field alone
    /// </summary>
    PriorityOnly,
    
    /// <summary>
    /// Combine ROI and priority (default)
    /// </summary>
    RoiAndPriority,
    
    /// <summary>
    /// Prioritize by potential impact percentage
    /// </summary>
    ImpactFirst,
    
    /// <summary>
    /// Prioritize by lowest implementation cost
    /// </summary>
    LowEffortFirst
}
