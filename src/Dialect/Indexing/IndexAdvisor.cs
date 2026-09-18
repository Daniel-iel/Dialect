namespace Dialect.Core.Indexing;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Analyzes compiled SQL queries to recommend beneficial indexes.
/// Examines WHERE clauses, JOIN conditions, and ORDER BY to suggest indexes.
/// </summary>
public abstract class IndexAdvisor
{
    /// <summary>
    /// Analyzes a compiled query and recommends indexes.
    /// </summary>
    /// <param name="query">The compiled query with SQL and parameters</param>
    /// <param name="dialect">The SQL dialect for dialect-specific recommendations</param>
    /// <returns>Index recommendations ordered by estimated benefit (highest first)</returns>
    public abstract IReadOnlyList<IndexRecommendation> Analyze(CompiledQuery query, ISqlDialect dialect);

    /// <summary>
    /// Scores the benefit of an index based on multiple factors.
    /// </summary>
    /// <param name="selectivity">Distinct values / total rows (0.0 to 1.0, higher is better)</param>
    /// <param name="queryFrequency">Estimated queries per hour using this predicate</param>
    /// <param name="joinDepth">Nesting level in join tree (higher = more benefit from index)</param>
    /// <returns>Score from 0.0 to 1.0 representing index benefit</returns>
    protected decimal ScoreIndexBenefit(decimal selectivity, int queryFrequency, int joinDepth = 0)
    {
        // Selectivity (50%) + Query Frequency (30%) + Join Depth (20%)
        var selectivityScore = selectivity * 0.5m;
        var frequencyScore = (Math.Min(queryFrequency, 100) / 100m) * 0.3m;
        var joinScore = (Math.Min(joinDepth, 5) / 5m) * 0.2m;

        return Math.Min(selectivityScore + frequencyScore + joinScore, 1.0m);
    }

    /// <summary>
    /// Determines priority level based on estimated benefit score.
    /// </summary>
    protected IndexPriority GetPriority(decimal benefit)
    {
        return benefit switch
        {
            >= 0.8m => IndexPriority.Critical,
            >= 0.6m => IndexPriority.High,
            >= 0.4m => IndexPriority.Medium,
            _ => IndexPriority.Low
        };
    }
}
