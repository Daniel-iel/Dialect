namespace Dialect.PostgreSql;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;
using Dialect.PostgreSql.Rendering;
using Dialect.PostgreSql.Schema;
using Dialect.PostgreSql.Indexing;
using Dialect.PostgreSql.Versioning;

/// <summary>
/// PostgreSQL dialect implementation (PostgreSQL 13+).
/// Supports: ANSI SQL, parameter prefix :, identifier quote ".
/// </summary>
public sealed class PostgreSqlDialect : ISqlDialect
{
    public char IdentifierQuote => '"';
    public string ParameterPrefix => ":";

    public bool Supports(SqlFeature feature) => feature switch
    {
        SqlFeature.FullJoin => true,
        SqlFeature.Returning => true,
        SqlFeature.WindowFunctions => true,
        SqlFeature.CommonTableExpressions => true,
        SqlFeature.JsonOperations => true,
        SqlFeature.Upsert => true, // INSERT ... ON CONFLICT
        SqlFeature.StoredProcedures => false, // PostgreSQL uses functions only
        SqlFeature.Functions => true,
        _ => false
    };

    public string RenderFunction(string functionName, IReadOnlyList<string> argumentPlaceholders)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));

        return functionName.ToUpperInvariant() switch
        {
            "CONCAT" => $"CONCAT({string.Join(", ", argumentPlaceholders)})",
            "SUBSTRING" => $"SUBSTRING({string.Join(", ", argumentPlaceholders)})",
            "LENGTH" => $"LENGTH({argumentPlaceholders.FirstOrDefault()})",
            "UPPER" => $"UPPER({argumentPlaceholders.FirstOrDefault()})",
            "LOWER" => $"LOWER({argumentPlaceholders.FirstOrDefault()})",
            "COALESCE" => $"COALESCE({string.Join(", ", argumentPlaceholders)})",
            "NOW" => "NOW()",
            "CURRENT_TIMESTAMP" => "CURRENT_TIMESTAMP",
            _ => $"{functionName}({string.Join(", ", argumentPlaceholders)})"
        };
    }

    public IQueryRenderer CreateQueryRenderer() => new PostgreSqlQueryRenderer();
    public IRoutineRenderer CreateRoutineRenderer() => new PostgreSqlRoutineRenderer();
    public IMigrationRenderer CreateMigrationRenderer() => new PostgreSqlMigrationRenderer();
    public SchemaValidator CreateSchemaValidator() => new PostgreSqlSchemaValidator();

    public IndexAdvisor CreateIndexAdvisor() => new Indexing.PostgreSqlIndexAdvisor();

    public VersionDetector CreateVersionDetector() => new PostgreSqlVersionDetector();
}
