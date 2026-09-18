namespace Dialect.Core.DI;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Service interface for retrieving SQL dialects.
/// Abstraction over SqlDialectRegistry for dependency injection and testing.
/// </summary>
public interface ISqlDialectProvider
{
    /// <summary>
    /// Gets the default dialect configured via AddSqlFramework().
    /// </summary>
    /// <returns>The default dialect, or null if no default has been set</returns>
    ISqlDialect? GetDefaultDialect();

    /// <summary>
    /// Gets a dialect by its SqlProvider enum value.
    /// </summary>
    /// <param name="provider">The SqlProvider enum value</param>
    /// <returns>The registered dialect, or null if not found</returns>
    ISqlDialect? GetDialect(SqlProvider provider);

    /// <summary>
    /// Gets all registered dialects.
    /// </summary>
    /// <returns>Dictionary of all registered dialects by provider</returns>
    IReadOnlyDictionary<SqlProvider, ISqlDialect> GetAllDialects();
}
