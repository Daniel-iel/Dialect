using Dialect.Samples.Services;
using MySqlConnector;
using System.Data.Common;

namespace Dialect.Samples.Executors;

/// <summary>
/// MySQL specific query executor.
/// Implements dialect-specific behaviors for MySQL via BaseQueryExecutor template method.
/// </summary>
public class MySqlExecutor : BaseQueryExecutor
{
    public MySqlExecutor(string connectionString) : base(connectionString)
    {
    }

    public override string GetDialectName() => "MySQL";

    /// <summary>
    /// Create MySQL database connection.
    /// </summary>
    protected override DbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    /// <summary>
    /// Add parameter to MySQL command with @-prefixed name.
    /// </summary>
    protected override void AddParameterToCommand(DbCommand command, string paramName, object? paramValue)
    {
        // Ensure parameter name has @ prefix
        var mysqlParamName = paramName.StartsWith("@") ? paramName : "@" + paramName;

        var mysqlCommand = command as MySqlCommand
            ?? throw new InvalidOperationException("Expected MySqlCommand");

        mysqlCommand.Parameters.AddWithValue(mysqlParamName, paramValue ?? DBNull.Value);
    }

    /// <summary>
    /// MySQL uses @pX format for named parameters.
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
