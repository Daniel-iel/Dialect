namespace Dialect.SqlServer.Indexing;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Indexing;

/// <summary>
/// SQL Server-specific index advisor.
/// Analyzes queries for beneficial indexes.
/// Supports INCLUDE (covering) indexes and filtered indexes.
/// </summary>
public class SqlServerIndexAdvisor : IndexAdvisor
{
    public override IReadOnlyList<IndexRecommendation> Analyze(CompiledQuery query, ISqlDialect dialect)
    {
        // MVP: Basic implementation returns no recommendations
        // Future: Parse query AST to analyze WHERE, JOIN, ORDER BY clauses
        return new List<IndexRecommendation>();
    }
}
