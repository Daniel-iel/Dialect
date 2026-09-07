namespace Dialect.Samples.Utilities;

using Dialect.Core.Dialects;
using Dialect.MySql;
using Dialect.PostgreSql;
using Dialect.SqlServer;

/// <summary>
/// Helper class to manage dialect instances and DI setup.
/// </summary>
public class DialectHelper
{
    private readonly Dictionary<string, ISqlDialect> _dialects;

    public DialectHelper()
    {
        _dialects = new Dictionary<string, ISqlDialect>
        {
            ["SQL Server"] = new SqlServerDialect(),
            ["PostgreSQL"] = new PostgreSqlDialect(),
            ["MySQL"] = new MySqlDialect()
        };
    }

    /// <summary>
    /// Get all available dialects.
    /// </summary>
    public IEnumerable<KeyValuePair<string, ISqlDialect>> GetAllDialects()
    {
        return _dialects;
    }

    /// <summary>
    /// Get a specific dialect by name.
    /// </summary>
    public ISqlDialect GetDialect(string name)
    {
        if (_dialects.TryGetValue(name, out var dialect))
        {
            return dialect;
        }

        throw new ArgumentException($"Unknown dialect: {name}. Available: {string.Join(", ", _dialects.Keys)}");
    }
}
