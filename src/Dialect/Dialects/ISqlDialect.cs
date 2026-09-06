namespace Dialect.Core.Dialects;

using Dialect.Core.AST;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;

/// <summary>
/// Defines the contract for SQL dialect implementations.
/// Dialects are responsible for rendering AST to dialect-specific SQL.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Gets the character used to quote identifiers (e.g., '[' for SQL Server, '"' for PostgreSQL, '`' for MySQL).
    /// </summary>
    char IdentifierQuote { get; }

    /// <summary>
    /// Gets the parameter prefix for this dialect (e.g., '@' for SQL Server, ':' for PostgreSQL, '?' for MySQL).
    /// </summary>
    string ParameterPrefix { get; }

    /// <summary>
    /// Determines whether this dialect supports a specific SQL feature.
    /// </summary>
    bool Supports(SqlFeature feature);

    /// <summary>
    /// Renders a SQL function call with dialect-specific translation.
    /// Returns the dialect-specific SQL representation (e.g., CONCAT vs || vs +).
    /// </summary>
    string RenderFunction(string functionName, IReadOnlyList<string> argumentPlaceholders);

    /// <summary>
    /// Creates a renderer for query statements.
    /// </summary>
    IQueryRenderer CreateQueryRenderer();

    /// <summary>
    /// Creates a renderer for routine calls (procedures/functions).
    /// </summary>
    IRoutineRenderer CreateRoutineRenderer();

    /// <summary>
    /// Creates a renderer for migration steps.
    /// </summary>
    IMigrationRenderer CreateMigrationRenderer();

    /// <summary>
    /// Creates a validator for schema definitions and migrations.
    /// </summary>
    SchemaValidator CreateSchemaValidator();

    /// <summary>
    /// Creates an advisor for index recommendations based on queries.
    /// </summary>
    IndexAdvisor CreateIndexAdvisor();

    /// <summary>
    /// Creates a version detector for determining database capabilities.
    /// </summary>
    VersionDetector CreateVersionDetector();
}

