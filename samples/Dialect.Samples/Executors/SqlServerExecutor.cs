using System.Data;
using System.Data.Common;
using Dialect.Samples.Services;
using Microsoft.Data.SqlClient;

namespace Dialect.Samples.Executors;

/// <summary>
/// SQL Server specific query executor.
/// Implements dialect-specific behaviors for SQL Server via BaseQueryExecutor template method.
/// </summary>
public class SqlServerExecutor : BaseQueryExecutor
{
    public SqlServerExecutor(string connectionString) : base(connectionString)
    {
    }

    public override string GetDialectName() => "SQL Server";

    /// <summary>
    /// Create SQL Server database connection.
    /// </summary>
    protected override DbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    /// <summary>
    /// Add parameter to SQL Server command with @-prefixed name.
    /// </summary>
    protected override void AddParameterToCommand(DbCommand command, string paramName, object? paramValue)
    {
        // Ensure parameter name has @ prefix
        var sqlParamName = paramName.StartsWith("@") ? paramName : "@" + paramName;
        
        var sqlCommand = command as SqlCommand
            ?? throw new InvalidOperationException("Expected SqlCommand");
        
        sqlCommand.Parameters.AddWithValue(sqlParamName, paramValue ?? DBNull.Value);
    }

    /// <summary>
    /// SQL Server uses @pX format for named parameters.
    /// Ensures all parameter names are properly prefixed.
    /// </summary>
    protected override Dictionary<string, object?> ConvertParameters(Dictionary<string, object?> parameters)
    {
        return ParameterConverter.ConvertParameters(
            new Core.AST.CompiledQuery("", parameters),
            GetDialectName()
        );
    }
}

