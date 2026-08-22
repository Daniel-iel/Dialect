namespace Dialect.Core.Query;

/// <summary>
/// Represents a parsed SQL clause (WHERE, JOIN, ORDER BY, GROUP BY, etc).
/// </summary>
public record ParsedClause(
    string Type,           // WHERE, JOIN, ORDER_BY, GROUP_BY, etc.
    string RawSql,         // Original clause text
    string[] Columns,      // Columns referenced in clause
    string[] Tables,       // Tables referenced in clause
    int Complexity         // Nesting level / complexity score
);

/// <summary>
/// Represents a parsed SQL query with extracted components.
/// </summary>
public record ParsedQuery(
    string SelectClause,
    string[] SelectColumns,
    string FromClause,
    string[] FromTables,
    ParsedClause[] WhereClauses,
    ParsedClause[] JoinClauses,
    ParsedClause[] GroupByClauses,
    ParsedClause[] OrderByClauses,
    decimal SelectivityEstimate,    // 0.0 to 1.0, estimated rows returned
    int QueryComplexity              // Overall complexity score
)
{
    /// <summary>
    /// Gets all columns referenced in query.
    /// </summary>
    public string[] GetAllColumns()
    {
        var columns = new HashSet<string>();
        foreach (var col in SelectColumns) columns.Add(col);
        foreach (var clause in WhereClauses)
            foreach (var col in clause.Columns) columns.Add(col);
        foreach (var clause in JoinClauses)
            foreach (var col in clause.Columns) columns.Add(col);
        return columns.ToArray();
    }

    /// <summary>
    /// Gets all tables referenced in query.
    /// </summary>
    public string[] GetAllTables()
    {
        var tables = new HashSet<string>();
        foreach (var tbl in FromTables) tables.Add(tbl);
        foreach (var clause in JoinClauses)
            foreach (var tbl in clause.Tables) tables.Add(tbl);
        return tables.ToArray();
    }
};
