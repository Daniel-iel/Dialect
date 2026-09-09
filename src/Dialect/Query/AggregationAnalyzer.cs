namespace Dialect.Core.Query;

/// <summary>
/// Analyzes GROUP BY and aggregation patterns for optimization.
/// </summary>
public class AggregationAnalyzer
{
    /// <summary>
    /// Analyzes a GROUP BY clause for optimization.
    /// </summary>
    public AggregationAnalysis Analyze(string groupByClause, string[] aggregateFunctions)
    {
        var columns = ExtractGroupByColumns(groupByClause);
        var aggregates = ParseAggregateFunctions(aggregateFunctions);
        var hasIndex = CanUseIndex(columns);
        var isOptimal = IsOptimalGroupBy(columns, aggregates);

        return new AggregationAnalysis(
            GroupByClause: groupByClause,
            GroupColumns: columns,
            AggregateFunctions: aggregates,
            CanUseIndex: hasIndex,
            IsOptimal: isOptimal,
            Recommendation: GenerateRecommendation(columns, aggregates, hasIndex)
        );
    }

    private static string[] ExtractGroupByColumns(string clause)
    {
        if (string.IsNullOrEmpty(clause)) return Array.Empty<string>();

        return clause.Split(',')
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();
    }

    private AggregateFunction[] ParseAggregateFunctions(string[] functions)
    {
        return functions.Select(f =>
        {
            var lower = f.ToLower().Trim();
            var type = DetermineFunctionType(lower);
            var column = ExtractColumnFromFunction(f);

            return new AggregateFunction(
                OriginalFunction: f,
                Type: type,
                Column: column
            );
        }).ToArray();
    }

    private static string DetermineFunctionType(string function)
    {
        if (function.StartsWith("sum")) return "SUM";
        if (function.StartsWith("count")) return "COUNT";
        if (function.StartsWith("avg")) return "AVG";
        if (function.StartsWith("min")) return "MIN";
        if (function.StartsWith("max")) return "MAX";
        if (function.StartsWith("string_agg") || function.StartsWith("group_concat")) return "STRING_AGG";
        return "UNKNOWN";
    }

    private static string ExtractColumnFromFunction(string function)
    {
        var start = function.IndexOf('(');
        var end = function.LastIndexOf(')');

        if (start >= 0 && end > start)
        {
            return function.Substring(start + 1, end - start - 1).Trim();
        }

        return string.Empty;
    }

    private static bool CanUseIndex(string[] columns)
    {
        // Can use index if GROUP BY matches index prefix
        // e.g., if index on (col1, col2), can use for GROUP BY col1, col2
        return columns.Length > 0;
    }

    private static bool IsOptimalGroupBy(string[] columns, AggregateFunction[] aggregates)
    {
        // Optimal if:
        // 1. Grouping columns are indexed
        // 2. Not aggregating too many different columns
        // 3. Not using expensive aggregates like STRING_AGG without partitioning

        bool reasonableColumns = columns.Length <= 3;
        bool reasonableAggregates = aggregates.Length <= 5;
        bool noExpensiveAggs = !aggregates.Any(a => a.Type == "STRING_AGG");

        return reasonableColumns && reasonableAggregates && noExpensiveAggs;
    }

    private static string GenerateRecommendation(string[] columns, AggregateFunction[] aggregates, bool canUseIndex)
    {
        if (!canUseIndex)
            return "Consider indexing GROUP BY columns for faster grouping.";

        if (aggregates.Any(a => a.Type == "STRING_AGG"))
            return "STRING_AGG is expensive. Consider partitioning or using a separate aggregation step.";

        if (columns.Length > 3)
            return "Grouping by many columns can be expensive. Review if all are necessary.";

        return "GROUP BY appears well-optimized.";
    }
}

/// <summary>
/// Analysis result for GROUP BY and aggregations.
/// </summary>
public record AggregationAnalysis(
    string GroupByClause,
    string[] GroupColumns,
    AggregateFunction[] AggregateFunctions,
    bool CanUseIndex,
    bool IsOptimal,
    string Recommendation
);

/// <summary>
/// Represents a parsed aggregate function.
/// </summary>
public record AggregateFunction(
    string OriginalFunction,
    string Type,           // SUM, COUNT, AVG, MIN, MAX, STRING_AGG
    string Column          // Column being aggregated
);
