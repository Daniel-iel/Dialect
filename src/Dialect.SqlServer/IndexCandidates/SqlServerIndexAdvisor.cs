using Dialect.Core.IndexCandidates;
using IndexCandidateAdvisorBase = Dialect.Core.IndexCandidates.IndexCandidateAdvisor;

namespace Dialect.SqlServer.IndexCandidates;

/// <summary>
/// SQL Server index advisor with clustered/nonclustered strategies
/// </summary>
public class SqlServerIndexAdvisor : IndexCandidateAdvisorBase
{
    public override IReadOnlyList<IndexCandidate> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null)
    {
        var candidates = new List<IndexCandidate>();

        // Extract tables and filter columns
        var tables = ExtractTableNames(queryText);
        var filterColumns = ExtractFilterColumns(queryText);

        // Generate index candidates for WHERE clause filters
        foreach (var table in tables)
        {
            if (filterColumns.Count > 0)
            {
                var indexCandidate = new IndexCandidate(
                    CandidateId: $"SQLSERVER_FILTER_{table}",
                    TableName: table,
                    Columns: filterColumns.Take(3).ToList(),  // Limit to 3 columns
                    IndexType: "Nonclustered",
                    IncludeColumns: new List<string>(),
                    PartialPredicate: null,
                    Reason: $"Filter columns in WHERE clause on {table}",
                    ImprovementPercentage: 25,
                    EstimatedSizeKb: EstimateIndexSizeKb(filterColumns.Count, 8, 1000000),
                    FrequencyScore: 4,
                    Priority: CalculatePriority(25, 4),
                    RoiScore: CalculateRoiScore(25, 3),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string>(),
                    DialectOptions: new Dictionary<string, string>
                    {
                        { "FILLFACTOR", "80" },
                        { "STATISTICS_NORECOMPUTE", "OFF" }
                    }
                );

                candidates.Add(indexCandidate);
            }
        }

        // Detect large table scans
        if (queryText.Contains("SELECT *", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var table in tables)
            {
                var coveringCandidate = new IndexCandidate(
                    CandidateId: $"SQLSERVER_COVERING_{table}",
                    TableName: table,
                    Columns: filterColumns.Count > 0 ? filterColumns.Take(2).ToList() : new List<string> { "Id" },
                    IndexType: "Nonclustered",
                    IncludeColumns: new List<string> { "CreatedAt", "UpdatedAt" },
                    PartialPredicate: null,
                    Reason: $"Covering index to avoid key lookups on {table}",
                    ImprovementPercentage: 15,
                    EstimatedSizeKb: EstimateIndexSizeKb(filterColumns.Count + 2, 8, 1000000),
                    FrequencyScore: 3,
                    Priority: CalculatePriority(15, 3),
                    RoiScore: CalculateRoiScore(15, 3),
                    BenefitingQueries: new List<string> { queryText },
                    Warnings: new List<string> { "Verify column selection to minimize index size" },
                    DialectOptions: new Dictionary<string, string>()
                );

                candidates.Add(coveringCandidate);
            }
        }

        // Deduplicate and return
        return DeduplicateCandidates(candidates);
    }

    protected override string GenerateCreateIndexStatement(IndexCandidate candidate)
    {
        var columnList = string.Join(", ", candidate.Columns.Select(c => $"[{c}]"));
        var includeClause = candidate.IncludeColumns.Count > 0
            ? $" INCLUDE ({string.Join(", ", candidate.IncludeColumns.Select(c => $"[{c}]"))})"
            : "";

        var options = "";
        if (candidate.DialectOptions.ContainsKey("FILLFACTOR"))
            options += $" WITH (FILLFACTOR = {candidate.DialectOptions["FILLFACTOR"]})";

        return $"CREATE {candidate.IndexType.ToUpper()} INDEX [IX_{candidate.TableName}_{string.Join("_", candidate.Columns)}] " +
               $"ON [{candidate.TableName}] ({columnList}){includeClause}{options};";
    }
}
