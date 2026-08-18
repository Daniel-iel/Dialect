namespace Dialect.SqlServer;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;
using Dialect.SqlServer.Rendering;
using Dialect.SqlServer.Schema;
using Dialect.SqlServer.Indexing;
using Dialect.SqlServer.Versioning;

/// <summary>
/// SQL Server dialect implementation (SQL Server 2019+).
/// Supports: TSQL syntax, parameter prefix @, identifier quote [].
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
{
    public char IdentifierQuote => '[';
    public string ParameterPrefix => "@";

    public bool Supports(SqlFeature feature) => feature switch
    {
        SqlFeature.FullJoin => true,
        SqlFeature.Returning => false, // SQL Server doesn't have RETURNING, uses OUTPUT
        SqlFeature.WindowFunctions => true,
        SqlFeature.CommonTableExpressions => true,
        SqlFeature.JsonOperations => true,
        SqlFeature.Upsert => true, // MERGE statement
        SqlFeature.StoredProcedures => true,
        SqlFeature.Functions => true,
        _ => false
    };

    public string RenderFunction(string functionName, IReadOnlyList<string> argumentPlaceholders)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));

        // Standard SQL functions that work across all dialects
        return functionName.ToUpperInvariant() switch
        {
            "CONCAT" => $"CONCAT({string.Join(", ", argumentPlaceholders)})",
            "SUBSTRING" => $"SUBSTRING({string.Join(", ", argumentPlaceholders)})",
            "LENGTH" => $"LEN({argumentPlaceholders.FirstOrDefault()})",
            "UPPER" => $"UPPER({argumentPlaceholders.FirstOrDefault()})",
            "LOWER" => $"LOWER({argumentPlaceholders.FirstOrDefault()})",
            "COALESCE" => $"COALESCE({string.Join(", ", argumentPlaceholders)})",
            "NOW" => "GETDATE()",
            "CURRENT_TIMESTAMP" => "CURRENT_TIMESTAMP",
            _ => $"{functionName}({string.Join(", ", argumentPlaceholders)})"
        };
    }

    public IQueryRenderer CreateQueryRenderer() => new SqlServerQueryRenderer();
    public IRoutineRenderer CreateRoutineRenderer() => new SqlServerRoutineRenderer();
    public IMigrationRenderer CreateMigrationRenderer() => new SqlServerMigrationRenderer();
    public SchemaValidator CreateSchemaValidator() => new SqlServerSchemaValidator();
    
    public IndexAdvisor CreateIndexAdvisor() => new Indexing.SqlServerIndexAdvisor();
    
    public VersionDetector CreateVersionDetector() => new SqlServerVersionDetector();
}
