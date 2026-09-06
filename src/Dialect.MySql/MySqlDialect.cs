namespace Dialect.MySql;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;
using Dialect.MySql.Rendering;
using Dialect.MySql.Schema;
using Dialect.MySql.Indexing;
using Dialect.MySql.Versioning;

/// <summary>
/// MySQL dialect implementation (MySQL 8+).
/// Supports: MySQL syntax, parameter prefix ?, identifier quote `.
/// Note: FULL OUTER JOIN is not supported natively.
/// </summary>
public sealed class MySqlDialect : ISqlDialect
{
    public char IdentifierQuote => '`';
    public string ParameterPrefix => "?";

    public bool Supports(SqlFeature feature) => feature switch
    {
        SqlFeature.FullJoin => false, // MySQL doesn't support FULL OUTER JOIN
        SqlFeature.Returning => false,
        SqlFeature.WindowFunctions => true,
        SqlFeature.CommonTableExpressions => true,
        SqlFeature.JsonOperations => true,
        SqlFeature.Upsert => true, // INSERT ... ON DUPLICATE KEY UPDATE
        SqlFeature.StoredProcedures => true,
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

    public IQueryRenderer CreateQueryRenderer() => new MySqlQueryRenderer();
    public IRoutineRenderer CreateRoutineRenderer() => new MySqlRoutineRenderer();
    public IMigrationRenderer CreateMigrationRenderer() => new MySqlMigrationRenderer();
    public SchemaValidator CreateSchemaValidator() => new MySqlSchemaValidator();

    public IndexAdvisor CreateIndexAdvisor() => new Indexing.MySqlIndexAdvisor();

    public VersionDetector CreateVersionDetector() => new MySqlVersionDetector();
}
