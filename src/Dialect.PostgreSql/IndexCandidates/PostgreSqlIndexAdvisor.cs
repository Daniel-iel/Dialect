using Dialect.Core.IndexCandidates;
using IndexCandidateAdvisorBase = Dialect.Core.IndexCandidates.IndexCandidateAdvisor;

namespace Dialect.PostgreSql.IndexCandidates;

/// <summary>
/// PostgreSQL index advisor with partial/BRIN strategies
/// </summary>
public class PostgreSqlIndexAdvisor : IndexCandidateAdvisorBase
{
    public override IReadOnlyList<IndexCandidate> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var candidates = new List<IndexCandidate>();

        var tables = ExtractTableNames(queryText);
        var filterColumns = ExtractFilterColumns(queryText);

        // Standard B-tree index for WHERE filters
        foreach (var table in tables)
        {
            if (filterColumns.Count > 0)
            {
                var candidate = new IndexCandidate(
                    CandidateId: $"POSTGRES_BTREE_{table}",
                    TableName: table,
                    Columns: filterColumns.Take(3).ToList(),
                    IndexType: "BTREE",
                    IncludeColumns: new List<string>(),
                    PartialPredicate: null,
                    Reason: $"B-tree index for filter columns on {table}",
                    ImprovementPercentage: 28,
                    EstimatedSizeKb: EstimateIndexSizeKb(filterColumns.Count, 8, 1000000),
                    FrequencyScore: 4,
                    Priority: CalculatePriority(28, 4),
                    RoiScore: CalculateRoiScore(28, 2),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string>(),
                    DialectOptions: new Dictionary<string, string>()
                );

                candidates.Add(candidate);
            }
        }

        // Partial index for status/state columns (common pattern)
        if (queryText.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var table in tables)
            {
                var partialCandidate = new IndexCandidate(
                    CandidateId: $"POSTGRES_PARTIAL_{table}",
                    TableName: table,
                    Columns: new List<string> { "status", "created_at" },
                    IndexType: "Partial",
                    IncludeColumns: new List<string>(),
                    PartialPredicate: "WHERE status = 'active'",
                    Reason: $"Partial index for active records on {table}",
                    ImprovementPercentage: 35,
                    EstimatedSizeKb: EstimateIndexSizeKb(2, 8, 100000),  // Smaller due to predicate
                    FrequencyScore: 5,
                    Priority: CalculatePriority(35, 5),
                    RoiScore: CalculateRoiScore(35, 2),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string> { "Partial index only useful if many inactive records" },
                    DialectOptions: new Dictionary<string, string>()
                );

                candidates.Add(partialCandidate);
            }
        }

        // BRIN index for large sequential tables (time-series data)
        var brinCandidate = new IndexCandidate(
            CandidateId: $"POSTGRES_BRIN_{tables.FirstOrDefault() ?? "table"}",
            TableName: tables.FirstOrDefault() ?? "table",
            Columns: new List<string> { "created_at" },
            IndexType: "BRIN",
            IncludeColumns: new List<string>(),
            PartialPredicate: null,
            Reason: "BRIN index for time-series data with natural ordering",
            ImprovementPercentage: 40,
            EstimatedSizeKb: 256,  // BRIN indexes are very compact
            FrequencyScore: 3,
            Priority: CalculatePriority(40, 3),
            RoiScore: CalculateRoiScore(40, 1),
            BenefitingQueries: new List<string> { queryText },
            Warnings: new List<string> { "BRIN best for time-series or sequential INSERT patterns" },
            DialectOptions: new Dictionary<string, string> { { "pages_per_range", "128" } }
        );

        candidates.Add(brinCandidate);
        return DeduplicateCandidates(candidates);
    }

    protected override string GenerateCreateIndexStatement(IndexCandidate candidate)
    {
        var columnList = string.Join(", ", candidate.Columns);
        var indexType = candidate.IndexType.ToUpper();
        var partialClause = !string.IsNullOrEmpty(candidate.PartialPredicate)
            ? $" {candidate.PartialPredicate}"
            : "";

        return $"CREATE INDEX CONCURRENTLY ix_{candidate.TableName}_{string.Join("_", candidate.Columns)} " +
               $"ON {candidate.TableName} USING {indexType} ({columnList}){partialClause};";
    }
}
