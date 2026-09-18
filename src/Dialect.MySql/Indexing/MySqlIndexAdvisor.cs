namespace Dialect.MySql.Indexing;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Indexing;

/// <summary>
/// MySQL-specific index advisor.
/// Analyzes queries for beneficial indexes.
/// Supports FULLTEXT indexes and BTREE/HASH variants.
/// </summary>
public class MySqlIndexAdvisor : IndexAdvisor
{
    public override IReadOnlyList<IndexRecommendation> Analyze(CompiledQuery query, ISqlDialect dialect)
    {
        // MVP: Basic implementation returns no recommendations
        // Future: Parse query AST to analyze WHERE, JOIN, ORDER BY clauses
        return new List<IndexRecommendation>();
    }
}
