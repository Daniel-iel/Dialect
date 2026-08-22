namespace Dialect.Core.Query;

/// <summary>
/// Parses SQL queries to extract optimization-relevant information.
/// Analyzes SELECT, WHERE, JOIN, GROUP BY, ORDER BY clauses.
/// </summary>
public abstract class QueryParser
{
    /// <summary>
    /// Parses a SQL query string and extracts component analysis.
    /// </summary>
    public abstract ParsedQuery Parse(string sql);

    /// <summary>
    /// Estimates selectivity (0.0 to 1.0) of WHERE clause predicates.
    /// Higher = fewer rows returned; 1.0 = all rows, 0.01 = 1%.
    /// </summary>
    protected decimal EstimateSelectivity(string[] predicates)
    {
        if (predicates.Length == 0) return 1.0m;

        // MVP: Simple heuristic
        // Each equality predicate reduces by ~10%
        // Each inequality (>, <, LIKE) reduces by ~30%
        decimal selectivity = 1.0m;

        foreach (var pred in predicates)
        {
            var lower = pred.ToLower();
            if (lower.Contains("=") && !lower.Contains("<") && !lower.Contains(">"))
                selectivity *= 0.1m;  // Equality: high selectivity
            else if (lower.Contains("between"))
                selectivity *= 0.2m;  // Range: moderate selectivity
            else if (lower.Contains("in"))
                selectivity *= 0.3m;  // IN list: lower selectivity
            else
                selectivity *= 0.5m;  // Other: unknown
        }

        return Math.Max(selectivity, 0.001m);  // Minimum 0.1%
    }

    /// <summary>
    /// Counts nesting depth (for subqueries and complex expressions).
    /// Used to score query complexity.
    /// </summary>
    protected int CountNestingDepth(string sql)
    {
        int depth = 0;
        int maxDepth = 0;

        foreach (var ch in sql)
        {
            if (ch == '(') { depth++; maxDepth = Math.Max(maxDepth, depth); }
            else if (ch == ')') depth--;
        }

        return maxDepth;
    }

    /// <summary>
    /// Calculates overall query complexity score (0-100).
    /// Higher = more complex.
    /// </summary>
    protected int CalculateComplexity(ParsedQuery query)
    {
        int score = 0;

        // Number of tables (join complexity)
        score += query.GetAllTables().Length * 5;

        // Number of conditions
        score += query.WhereClauses.Length * 3;

        // Join complexity
        score += query.JoinClauses.Length * 10;

        // Aggregation complexity
        score += query.GroupByClauses.Length * 8;

        // Nesting
        score += query.QueryComplexity * 2;

        return Math.Min(score, 100);
    }
}
