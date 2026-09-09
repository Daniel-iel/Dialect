namespace Dialect.Core.Query;

/// <summary>
/// Analyzes JOIN clauses to determine join complexity and optimization opportunities.
/// </summary>
public class JoinAnalyzer
{
    /// <summary>
    /// Analyzes a JOIN clause for optimization.
    /// </summary>
    public JoinAnalysis Analyze(string joinClause)
    {
        var lower = joinClause.ToLower().Trim();
        var joinType = DetermineJoinType(lower);
        var (leftTable, rightTable) = ExtractTableNames(joinClause);
        var (joinColumn, joinCondition) = ExtractJoinCondition(joinClause);
        var isOptimal = IsOptimalJoin(joinType, joinCondition);

        return new JoinAnalysis(
            JoinClause: joinClause,
            JoinType: joinType,
            LeftTable: leftTable,
            RightTable: rightTable,
            JoinColumn: joinColumn,
            JoinCondition: joinCondition,
            IsOptimal: isOptimal,
            Recommendation: GenerateRecommendation(joinType, isOptimal)
        );
    }

    private static string DetermineJoinType(string clause)
    {
        if (clause.Contains("inner join")) return "INNER_JOIN";
        if (clause.Contains("left join") || clause.Contains("left outer join")) return "LEFT_JOIN";
        if (clause.Contains("right join") || clause.Contains("right outer join")) return "RIGHT_JOIN";
        if (clause.Contains("full join") || clause.Contains("full outer join")) return "FULL_JOIN";
        if (clause.Contains("cross join")) return "CROSS_JOIN";
        return "UNKNOWN_JOIN";
    }

    private static (string leftTable, string rightTable) ExtractTableNames(string clause)
    {
        var words = clause.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Simple extraction: "table1 JOIN table2"
        string leftTable = string.Empty;
        string rightTable = string.Empty;

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].ToLower().EndsWith("join"))
            {
                if (i > 0) leftTable = words[i - 1];
                if (i + 1 < words.Length) rightTable = words[i + 1];
                break;
            }
        }

        return (leftTable, rightTable);
    }

    private (string column, string condition) ExtractJoinCondition(string clause)
    {
        if (!clause.Contains("on", StringComparison.OrdinalIgnoreCase))
            return (string.Empty, string.Empty);

        var parts = clause.Split(new[] { "on", "ON" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return (string.Empty, string.Empty);

        var condition = parts[1].Trim();
        var column = ExtractColumnFromCondition(condition);
        return (column, condition);
    }

    private static string ExtractColumnFromCondition(string condition)
    {
        var parts = condition.Split('=');
        if (parts.Length >= 1)
            return parts[0].Trim();
        return string.Empty;
    }

    private static bool IsOptimalJoin(string joinType, string joinCondition)
    {
        // Optimal if: using indexed columns and INNER/LEFT JOIN
        bool typeOk = joinType == "INNER_JOIN" || joinType == "LEFT_JOIN";
        bool conditionOk = !string.IsNullOrEmpty(joinCondition) && joinCondition.Contains("=");

        return typeOk && conditionOk;
    }

    private static string GenerateRecommendation(string joinType, bool isOptimal)
    {
        if (isOptimal)
            return "Join is well-optimized. Ensure join columns are indexed.";

        if (joinType == "FULL_JOIN")
            return "FULL JOIN is less efficient. Consider rewriting with UNION of LEFT and RIGHT joins.";

        if (joinType == "CROSS_JOIN")
            return "CROSS JOIN can be very expensive. Verify it's necessary.";

        return "Consider adding indexes on join columns to improve performance.";
    }
}

/// <summary>
/// Analysis result for a JOIN clause.
/// </summary>
public record JoinAnalysis(
    string JoinClause,
    string JoinType,
    string LeftTable,
    string RightTable,
    string JoinColumn,
    string JoinCondition,
    bool IsOptimal,
    string Recommendation
);
