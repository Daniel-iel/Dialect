namespace Dialect.Core.DI;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Default implementation of ISqlDialectProvider.
/// Wraps the SqlDialectRegistry singleton for dependency injection.
/// </summary>
internal sealed class DefaultSqlDialectProvider : ISqlDialectProvider
{
    /// <summary>
    /// Gets the default dialect from the registry.
    /// </summary>
    public ISqlDialect? GetDefaultDialect()
    {
        return SqlDialectRegistry.Instance.GetDefault();
    }

    /// <summary>
    /// Gets a dialect by its SqlProvider enum value from the registry.
    /// </summary>
    public ISqlDialect? GetDialect(SqlProvider provider)
    {
        return SqlDialectRegistry.Instance.GetDialect(provider);
    }

    /// <summary>
    /// Gets all registered dialects from the registry.
    /// </summary>
    public IReadOnlyDictionary<SqlProvider, ISqlDialect> GetAllDialects()
    {
        return SqlDialectRegistry.Instance.GetAllDialects();
    }
}
