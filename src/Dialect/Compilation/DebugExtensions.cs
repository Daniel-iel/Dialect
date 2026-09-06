namespace Dialect.Core.Compilation;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using System.Text;

/// <summary>
/// Extension methods for debugging compiled SQL queries with inline parameter values.
/// </summary>
public static class DebugExtensions
{
    /// <summary>
    /// Converts a CompiledQuery to a debug string with inline parameter values.
    /// Useful for logging and debugging query execution.
    /// </summary>
    public static string ToDebugString(this CompiledQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        if (query.Parameters == null || query.Parameters.Count == 0)
            return query.Sql;

        var debugSql = new StringBuilder(query.Sql);

        // Sort parameters by their name/number to replace in reverse order
        // This prevents issues with parameter names that are substrings of others
        var sortedParams = query.Parameters
            .OrderByDescending(kvp => kvp.Key.Length)
            .ThenByDescending(kvp => kvp.Key)
            .ToList();

        foreach (var parameter in sortedParams)
        {
            var placeholder = parameter.Key;
            var value = FormatParameterValue(parameter.Value);
            debugSql.Replace(placeholder, value);
        }

        return debugSql.ToString();
    }

    /// <summary>
    /// Formats a parameter value for inline display in debug SQL.
    /// </summary>
    private static string FormatParameterValue(object? value)
    {
        if (value == null)
            return "NULL";

        return value switch
        {
            string s => $"'{EscapeSqlString(s)}'",
            bool b => b ? "1" : "0",
            byte b => b.ToString(),
            short s => s.ToString(),
            int i => i.ToString(),
            long l => l.ToString(),
            float f => f.ToString("G"),
            double d => d.ToString("G"),
            decimal dec => dec.ToString("G"),
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'",
            DateTimeOffset dto => $"'{dto.UtcDateTime:yyyy-MM-dd HH:mm:ss.fff}'",
            DateOnly date => $"'{date:yyyy-MM-dd}'",
            TimeOnly time => $"'{time:HH:mm:ss.fff}'",
            Guid g => $"'{g}'",
            byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
            _ => $"'{value}'"
        };
    }

    /// <summary>
    /// Escapes single quotes in SQL strings for inline display.
    /// </summary>
    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Converts a SelectStatement to a debug string with sample parameters.
    /// Useful for examining generated SQL structure.
    /// </summary>
    public static string ToDebugString(this SelectStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var compiled = statement.Compile(dialect);
        return compiled.ToDebugString();
    }

    /// <summary>
    /// Converts an InsertStatement to a debug string with sample parameters.
    /// </summary>
    public static string ToDebugString(this InsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var compiled = statement.Compile(dialect);
        return compiled.ToDebugString();
    }

    /// <summary>
    /// Converts an UpdateStatement to a debug string with sample parameters.
    /// </summary>
    public static string ToDebugString(this UpdateStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var compiled = statement.Compile(dialect);
        return compiled.ToDebugString();
    }

    /// <summary>
    /// Converts a DeleteStatement to a debug string with sample parameters.
    /// </summary>
    public static string ToDebugString(this DeleteStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var compiled = statement.Compile(dialect);
        return compiled.ToDebugString();
    }
}
