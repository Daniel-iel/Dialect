using System.Data;
using System.Data.Common;
using Npgsql;

namespace Dialect.Samples.Executors;

/// <summary>
/// PostgreSQL specific query executor.
/// Implements dialect-specific behaviors for PostgreSQL via BaseQueryExecutor template method.
/// </summary>
public class PostgreSqlExecutor : BaseQueryExecutor
{
    public PostgreSqlExecutor(string connectionString) : base(connectionString)
    {
    }

    public override string GetDialectName() => "PostgreSQL";

    /// <summary>
    /// Create PostgreSQL database connection.
    /// </summary>
    protected override DbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    /// <summary>
    /// Add parameter to PostgreSQL command with :-prefixed name.
    /// Npgsql supports both named parameters (:name) and positional ($1, $2, etc).
    /// We use named parameters for consistency across dialects.
    /// </summary>
    protected override void AddParameterToCommand(DbCommand command, string paramName, object? paramValue)
    {
        // Ensure parameter name has : prefix (PostgreSQL style)
        var pgParamName = paramName.StartsWith(":") ? paramName : ":" + paramName;
        
        var npgsqlCommand = command as NpgsqlCommand
            ?? throw new InvalidOperationException("Expected NpgsqlCommand");
        
        npgsqlCommand.Parameters.AddWithValue(pgParamName, paramValue ?? DBNull.Value);
    }

    /// <summary>
    /// PostgreSQL uses :pX format for named parameters.
    /// Ensures all parameter names are properly converted from source format.
    /// </summary>
    protected override Dictionary<string, object?> ConvertParameters(Dictionary<string, object?> parameters)
    {
        return ParameterConverter.ConvertParameters(
            new Core.AST.CompiledQuery("", parameters),
            GetDialectName()
        );
    }
}

