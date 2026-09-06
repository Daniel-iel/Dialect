namespace Dialect.Core.IndexCandidates;

/// <summary>
/// Base class for index advisor implementations
/// Analyzes queries to identify index opportunities
/// </summary>
public abstract class IndexCandidateAdvisor
{
    /// <summary>
    /// Generates index recommendations from query analysis
    /// </summary>
    public abstract IReadOnlyList<IndexCandidate> AnalyzeQuery(
        string queryText,
        IDictionary<string, object>? contextData = null);

    /// <summary>
    /// Protected helper: Generate index DDL for candidate
    /// </summary>
    protected abstract string GenerateCreateIndexStatement(IndexCandidate candidate);

    /// <summary>
    /// Protected helper: Validate index candidate
    /// </summary>
    protected bool ValidateCandidate(IndexCandidate candidate)
    {
        return candidate.IsValid &&
               candidate.Columns.Count > 0 &&
               candidate.FrequencyScore >= 1;
    }

    /// <summary>
    /// Protected helper: Calculate priority based on improvement and frequency
    /// </summary>
    protected int CalculatePriority(decimal improvement, int frequency)
    {
        var score = (improvement / 20) + (frequency * 1); // 0-5 + 1-5 = 1-10
        return (int)Math.Min(5, Math.Max(1, score / 2));
    }

    /// <summary>
    /// Protected helper: Deduplicate overlapping column suggestions
    /// </summary>
    protected List<IndexCandidate> DeduplicateCandidates(
        IEnumerable<IndexCandidate> candidates)
    {
        var unique = new Dictionary<string, IndexCandidate>();

        foreach (var candidate in candidates)
        {
            var key = GenerateCandidateKey(candidate);

            if (!unique.ContainsKey(key))
            {
                unique[key] = candidate;
            }
            else if (candidate.Priority > unique[key].Priority)
            {
                unique[key] = candidate;
            }
        }

        return unique.Values.ToList();
    }

    /// <summary>
    /// Generate unique key for deduplication
    /// </summary>
    private static string GenerateCandidateKey(IndexCandidate candidate)
    {
        var columnsKey = string.Join("_", candidate.Columns);
        return $"{candidate.TableName}_{columnsKey}_{candidate.IndexType}";
    }

    /// <summary>
    /// Protected helper: Calculate ROI score
    /// </summary>
    protected decimal CalculateRoiScore(decimal improvement, int complexity)
    {
        if (complexity == 0) return 0;
        return Math.Round(improvement / complexity, 2);
    }

    /// <summary>
    /// Protected helper: Estimate index size (in KB)
    /// </summary>
    protected long EstimateIndexSizeKb(
        int columnCount,
        int averageColumnWidthBytes,
        long estimatedRowCount)
    {
        // Rough estimation: (column_count * avg_width + overhead) * row_count / 1024
        const int overhead = 20; // bytes per row for index overhead
        var indexSizeBytes = (columnCount * averageColumnWidthBytes + overhead) * estimatedRowCount;
        return Math.Max(1, indexSizeBytes / 1024);
    }

    /// <summary>
    /// Protected helper: Extract table names from query
    /// </summary>
    protected List<string> ExtractTableNames(string queryText)
    {
        var tables = new List<string>();

        // Simple pattern matching - can be enhanced with actual parsing
        const string fromPattern = @"FROM\s+(\w+)";
        const string joinPattern = @"(?:INNER\s+|LEFT\s+|RIGHT\s+|FULL\s+)?JOIN\s+(\w+)";

        var fromMatches = System.Text.RegularExpressions.Regex.Matches(
            queryText, fromPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var joinMatches = System.Text.RegularExpressions.Regex.Matches(
            queryText, joinPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in fromMatches)
            if (match.Groups.Count > 1)
                tables.Add(match.Groups[1].Value);

        foreach (System.Text.RegularExpressions.Match match in joinMatches)
            if (match.Groups.Count > 1)
                tables.Add(match.Groups[1].Value);

        return tables.Distinct().ToList();
    }

    /// <summary>
    /// Protected helper: Extract columns from WHERE clause
    /// </summary>
    protected List<string> ExtractFilterColumns(string queryText)
    {
        var columns = new List<string>();

        // Simple pattern to find columns in WHERE clause
        const string wherePattern = @"WHERE\s+(.*?)(?:GROUP\s+BY|ORDER\s+BY|LIMIT|OFFSET|$)";
        var match = System.Text.RegularExpressions.Regex.Match(
            queryText, wherePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Groups.Count > 1)
        {
            var whereClause = match.Groups[1].Value;
            const string columnPattern = @"\b(\w+)\s*(?:=|<|>|<=|>=|!=|<>|LIKE|IN|BETWEEN)";
            var columnMatches = System.Text.RegularExpressions.Regex.Matches(whereClause, columnPattern);

            foreach (System.Text.RegularExpressions.Match colMatch in columnMatches)
                if (colMatch.Groups.Count > 1)
                    columns.Add(colMatch.Groups[1].Value);
        }

        return columns.Distinct().ToList();
    }
}
