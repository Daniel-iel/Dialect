namespace Dialect.Core.IndexCandidates;

/// <summary>
/// Represents a suggested index opportunity
/// </summary>
public record IndexCandidate(
    /// <summary>
    /// Unique identifier for this index candidate
    /// </summary>
    string CandidateId,
    
    /// <summary>
    /// Table name that would benefit from index
    /// </summary>
    string TableName,
    
    /// <summary>
    /// Columns to include in index (in order)
    /// </summary>
    IReadOnlyList<string> Columns,
    
    /// <summary>
    /// Index type: Clustered, Nonclustered, Partial, BRIN, etc.
    /// </summary>
    string IndexType,
    
    /// <summary>
    /// Columns to include without being part of search (covering index)
    /// </summary>
    IReadOnlyList<string> IncludeColumns,
    
    /// <summary>
    /// Predicate for partial indexes (PostgreSQL WHERE clause)
    /// </summary>
    string? PartialPredicate,
    
    /// <summary>
    /// Reason for this index recommendation
    /// </summary>
    string Reason,
    
    /// <summary>
    /// Estimated improvement percentage (0-100)
    /// </summary>
    decimal ImprovementPercentage,
    
    /// <summary>
    /// Estimated index size in kilobytes
    /// </summary>
    long EstimatedSizeKb,
    
    /// <summary>
    /// Frequency of query benefit (1-5 scale)
    /// </summary>
    int FrequencyScore,
    
    /// <summary>
    /// Priority for implementation (1-5, where 5 = highest)
    /// </summary>
    int Priority,
    
    /// <summary>
    /// Estimated ROI score (improvement / cost)
    /// </summary>
    decimal RoiScore,
    
    /// <summary>
    /// Queries that would benefit from this index
    /// </summary>
    IReadOnlyList<string> BenefitingQueries,
    
    /// <summary>
    /// Warnings or considerations
    /// </summary>
    IReadOnlyList<string> Warnings,
    
    /// <summary>
    /// Dialect-specific index options
    /// </summary>
    IReadOnlyDictionary<string, string> DialectOptions
)
{
    /// <summary>
    /// Validates index candidate data
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CandidateId) &&
        !string.IsNullOrWhiteSpace(TableName) &&
        Columns.Count > 0 &&
        !string.IsNullOrWhiteSpace(IndexType) &&
        ImprovementPercentage >= 0 && ImprovementPercentage <= 100 &&
        EstimatedSizeKb >= 0 &&
        FrequencyScore >= 1 && FrequencyScore <= 5 &&
        Priority >= 1 && Priority <= 5 &&
        RoiScore >= 0;
}
