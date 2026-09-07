namespace Dialect.Samples.Services;

using Dialect.Core.AST;

/// <summary>
/// Consolidates parameter conversion logic across all database dialects.
/// Converts CompiledQuery parameters to dialect-specific formats.
/// 
/// Dialects use different parameter prefix conventions:
/// - SQL Server: @p1, @p2 (named parameters with @ prefix)
/// - PostgreSQL: :p1, :p2 (named parameters with : prefix, or positional $1, $2)
/// - MySQL: @p1, @p2 (named parameters with @ prefix)
/// </summary>
public static class ParameterConverter
{
    /// <summary>
    /// Convert compiled query parameters to a dictionary suitable for database execution.
    /// Normalizes parameter names to ensure compatibility with each database dialect.
    /// </summary>
    /// <param name="compiledQuery">The compiled query containing parameters</param>
    /// <param name="dialectName">The target database dialect (e.g., "SQL Server", "PostgreSQL", "MySQL")</param>
    /// <returns>Dictionary with dialect-specific parameter names and values</returns>
    public static Dictionary<string, object?> ConvertParameters(
        CompiledQuery compiledQuery,
        string dialectName)
    {
        if (compiledQuery?.Parameters == null || compiledQuery.Parameters.Count == 0)
            return new Dictionary<string, object?>();

        return dialectName.ToLower() switch
        {
            "sql server" or "sqlserver" => ConvertForSqlServer(compiledQuery.Parameters),
            "postgresql" or "postgres" => ConvertForPostgreSQL(compiledQuery.Parameters),
            "mysql" => ConvertForMySQL(compiledQuery.Parameters),
            _ => throw new ArgumentException($"Unknown dialect: {dialectName}", nameof(dialectName))
        };
    }

    /// <summary>
    /// SQL Server: Ensure all parameter names start with @ prefix.
    /// SqlCommand expects named parameters in @pX format.
    /// </summary>
    private static Dictionary<string, object?> ConvertForSqlServer(IReadOnlyDictionary<string, object?> parameters)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var (key, value) in parameters)
        {
            var paramName = key.StartsWith("@") ? key : "@" + key;
            result[paramName] = value;
        }
        
        return result;
    }

    /// <summary>
    /// PostgreSQL: Keep parameter names in :pX format (named parameters).
    /// Npgsql supports named parameters directly without positional conversion.
    /// </summary>
    private static Dictionary<string, object?> ConvertForPostgreSQL(IReadOnlyDictionary<string, object?> parameters)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var (key, value) in parameters)
        {
            // Convert @p1 to :p1, or keep :p1 as-is
            var paramName = key.StartsWith("@") 
                ? ":" + key[1..]  // @p1 → :p1
                : key.StartsWith(":")
                    ? key           // :p1 → :p1 (no change)
                    : ":" + key;    // p1 → :p1
            
            result[paramName] = value;
        }
        
        return result;
    }

    /// <summary>
    /// MySQL: Ensure all parameter names start with @ prefix.
    /// MySqlCommand expects named parameters in @pX format.
    /// </summary>
    private static Dictionary<string, object?> ConvertForMySQL(IReadOnlyDictionary<string, object?> parameters)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var (key, value) in parameters)
        {
            var paramName = key.StartsWith("@") ? key : "@" + key;
            result[paramName] = value;
        }
        
        return result;
    }

    /// <summary>
    /// Convert parameters for Dapper (which strips all prefixes and uses positional mapping).
    /// Dapper's DynamicParameters expects parameter names without prefixes.
    /// </summary>
    /// <param name="parameters">Raw parameters from CompiledQuery</param>
    /// <returns>Dictionary with prefix-stripped parameter names</returns>
    public static Dictionary<string, object?> ConvertForDapper(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return new Dictionary<string, object?>();

        var result = new Dictionary<string, object?>();
        
        foreach (var (key, value) in parameters)
        {
            // Strip any leading @ or : prefix
            var paramName = key.StartsWith("@")
                ? key[1..]
                : key.StartsWith(":")
                    ? key[1..]
                    : key;

            result[paramName] = value;
        }

        return result;
    }

    /// <summary>
    /// Check if a parameter name needs conversion for a specific dialect.
    /// </summary>
    public static bool NeedsConversion(string paramName, string dialectName)
    {
        return dialectName.ToLower() switch
        {
            "sql server" or "sqlserver" => !paramName.StartsWith("@"),
            "postgresql" or "postgres" => !paramName.StartsWith(":"),
            "mysql" => !paramName.StartsWith("@"),
            _ => false
        };
    }
}
