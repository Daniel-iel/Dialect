namespace Dialect.Core.Query;

/// <summary>
/// Analyzes WHERE clause predicates to determine selectivity and index opportunities.
/// </summary>
public class PredicateAnalyzer
{
    /// <summary>
    /// Analyzes a predicate for selectivity and index suitability.
    /// </summary>
    public PredicateAnalysis Analyze(string predicate)
    {
        var lower = predicate.ToLower().Trim();
        var operatorType = DetermineOperatorType(lower);
        var isIndexable = IsIndexable(operatorType);
        var selectivity = EstimateSelectivity(operatorType);

        // Extract column and value
        var (column, value) = ExtractColumnAndValue(predicate);

        return new PredicateAnalysis(
            Predicate: predicate,
            Column: column,
            Value: value,
            OperatorType: operatorType,
            IsIndexable: isIndexable,
            Selectivity: selectivity,
            IndexPriority: CalculateIndexPriority(isIndexable, selectivity)
        );
    }

    private string DetermineOperatorType(string predicate)
    {
        if (predicate.Contains(" = ")) return "EQUALITY";
        if (predicate.Contains(" <> ") || predicate.Contains(" != ")) return "INEQUALITY";
        if (predicate.Contains(" < ") || predicate.Contains(" > ")) return "COMPARISON";
        if (predicate.Contains(" <= ") || predicate.Contains(" >= ")) return "RANGE";
        if (predicate.Contains(" between ")) return "BETWEEN";
        if (predicate.Contains(" in ")) return "IN_LIST";
        if (predicate.Contains(" like ")) return "LIKE";
        if (predicate.Contains(" is null")) return "IS_NULL";
        if (predicate.Contains(" is not null")) return "IS_NOT_NULL";
        return "UNKNOWN";
    }

    private bool IsIndexable(string operatorType)
    {
        return operatorType switch
        {
            "EQUALITY" => true,
            "COMPARISON" => true,
            "RANGE" => true,
            "BETWEEN" => true,
            "IN_LIST" => true,
            "LIKE" => false,  // LIKE with leading % not indexable
            "IS_NULL" => false,
            "IS_NOT_NULL" => false,
            _ => false
        };
    }

    private decimal EstimateSelectivity(string operatorType)
    {
        // Selectivity: percentage of rows matching predicate
        // Higher = fewer rows (more selective)
        return operatorType switch
        {
            "EQUALITY" => 0.1m,      // 10% - highly selective
            "BETWEEN" => 0.3m,       // 30% - moderate selectivity
            "IN_LIST" => 0.25m,      // 25% - list selectivity
            "RANGE" => 0.4m,         // 40% - lower selectivity
            "COMPARISON" => 0.4m,    // 40% - lower selectivity
            "LIKE" => 0.5m,          // 50% - low selectivity
            "IS_NULL" => 0.05m,      // 5% - usually few NULLs
            "IS_NOT_NULL" => 0.95m,  // 95% - usually not many NULLs
            _ => 0.5m
        };
    }

    private (string column, string value) ExtractColumnAndValue(string predicate)
    {
        var parts = predicate.Split(new[] { "=", "<", ">", " like ", " between ", " in " },
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1)
        {
            var column = parts[0].Trim();
            var value = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            return (column, value);
        }

        return (string.Empty, string.Empty);
    }

    private int CalculateIndexPriority(bool isIndexable, decimal selectivity)
    {
        if (!isIndexable) return 0;
        
        // Scale selectivity to priority (1-10)
        // Higher selectivity = higher priority for index
        return (int)(selectivity * 10);
    }
}

/// <summary>
/// Analysis result for a single predicate.
/// </summary>
public record PredicateAnalysis(
    string Predicate,
    string Column,
    string Value,
    string OperatorType,
    bool IsIndexable,
    decimal Selectivity,
    int IndexPriority
);
