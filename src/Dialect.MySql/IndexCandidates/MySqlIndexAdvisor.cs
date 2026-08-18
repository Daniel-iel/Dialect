using Dialect.Core.IndexCandidates;
using IndexCandidateAdvisorBase = Dialect.Core.IndexCandidates.IndexCandidateAdvisor;

namespace Dialect.MySql.IndexCandidates;

/// <summary>
/// MySQL index advisor with composite key strategies
/// </summary>
public class MySqlIndexAdvisor : IndexCandidateAdvisorBase
{
    public override IReadOnlyList<IndexCandidate> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var candidates = new List<IndexCandidate>();
        
        var tables = ExtractTableNames(queryText);
        var filterColumns = ExtractFilterColumns(queryText);
        
        // Composite index for multiple filter columns
        foreach (var table in tables)
        {
            if (filterColumns.Count >= 2)
            {
                var compositeCandidate = new IndexCandidate(
                    CandidateId: $"MYSQL_COMPOSITE_{table}",
                    TableName: table,
                    Columns: filterColumns.Take(3).ToList(),
                    IndexType: "Composite",
                    IncludeColumns: new List<string>(),
                    PartialPredicate: null,
                    Reason: $"Composite index for multiple filter conditions on {table}",
                    ImprovementPercentage: 32,
                    EstimatedSizeKb: EstimateIndexSizeKb(filterColumns.Count, 10, 1000000),
                    FrequencyScore: 4,
                    Priority: CalculatePriority(32, 4),
                    RoiScore: CalculateRoiScore(32, 2),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string> { "Column order matters: equality first, then range" },
                    DialectOptions: new Dictionary<string, string>
                    {
                        { "KEY_BLOCK_SIZE", "16" },
                        { "USING", "BTREE" }
                    }
                );
                
                candidates.Add(compositeCandidate);
            }
            else if (filterColumns.Count > 0)
            {
                // Single column index
                var singleCandidate = new IndexCandidate(
                    CandidateId: $"MYSQL_SINGLE_{table}",
                    TableName: table,
                    Columns: filterColumns.Take(1).ToList(),
                    IndexType: "Single",
                    IncludeColumns: new List<string>(),
                    PartialPredicate: null,
                    Reason: $"Single column index for filter on {table}",
                    ImprovementPercentage: 20,
                    EstimatedSizeKb: EstimateIndexSizeKb(1, 10, 1000000),
                    FrequencyScore: 3,
                    Priority: CalculatePriority(20, 3),
                    RoiScore: CalculateRoiScore(20, 2),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string>(),
                    DialectOptions: new Dictionary<string, string> { { "USING", "BTREE" } }
                );
                
                candidates.Add(singleCandidate);
            }
        }
        
        // Covering index with SELECT columns included
        if (queryText.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var table in tables)
            {
                var coveringCandidate = new IndexCandidate(
                    CandidateId: $"MYSQL_COVERING_{table}",
                    TableName: table,
                    Columns: filterColumns.Count > 0 ? filterColumns.Take(2).ToList() : new List<string> { "id" },
                    IndexType: "Covering",
                    IncludeColumns: new List<string> { "updated_at", "status" },
                    PartialPredicate: null,
                    Reason: $"Covering index to avoid table access on {table}",
                    ImprovementPercentage: 25,
                    EstimatedSizeKb: EstimateIndexSizeKb(filterColumns.Count + 2, 10, 1000000),
                    FrequencyScore: 3,
                    Priority: CalculatePriority(25, 3),
                    RoiScore: CalculateRoiScore(25, 2),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string> { "Verify column selection; larger indexes slow inserts" },
                    DialectOptions: new Dictionary<string, string>()
                );
                
                candidates.Add(coveringCandidate);
            }
        }
        
        return DeduplicateCandidates(candidates);
    }
    
    protected override string GenerateCreateIndexStatement(IndexCandidate candidate)
    {
        var columnList = string.Join(", ", candidate.Columns);
        var uniqueKeyword = candidate.IndexType == "Unique" ? "UNIQUE " : "";
        var usingClause = candidate.DialectOptions.ContainsKey("USING")
            ? $" USING {candidate.DialectOptions["USING"]}"
            : " USING BTREE";
        
        return $"CREATE {uniqueKeyword}INDEX `idx_{candidate.TableName}_{string.Join("_", candidate.Columns)}` " +
               $"ON `{candidate.TableName}` ({columnList}){usingClause};";
    }
}
