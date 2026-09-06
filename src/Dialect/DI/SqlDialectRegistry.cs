namespace Dialect.Core.DI;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Thread-safe singleton registry for SQL dialects.
/// Stores the default dialect configured via AddSqlFramework() and provides lookup by SqlProvider enum.
/// </summary>
public sealed class SqlDialectRegistry
{
    private static readonly Lazy<SqlDialectRegistry> _instance = 
        new Lazy<SqlDialectRegistry>(() => new SqlDialectRegistry(), LazyThreadSafetyMode.ExecutionAndPublication);

    private ISqlDialect? _defaultDialect;
    private readonly Dictionary<SqlProvider, ISqlDialect> _dialectsByProvider = new();
    private readonly ReaderWriterLockSlim _lock = new();

    private SqlDialectRegistry()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the registry.
    /// </summary>
    public static SqlDialectRegistry Instance => _instance.Value;

    /// <summary>
    /// Sets the default dialect that will be used by .Compile() and .Translate() methods
    /// when no explicit dialect parameter is provided.
    /// </summary>
    /// <param name="dialect">The dialect to set as default</param>
    /// <exception cref="ArgumentNullException">Thrown if dialect is null</exception>
    public void SetDefault(ISqlDialect dialect)
    {
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect), "Default dialect cannot be null");

        _lock.EnterWriteLock();
        try
        {
            _defaultDialect = dialect;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the default dialect.
    /// </summary>
    /// <returns>The default dialect, or null if no default has been set</returns>
    public ISqlDialect? GetDefault()
    {
        _lock.EnterReadLock();
        try
        {
            return _defaultDialect;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Registers a dialect for a specific SqlProvider enum value.
    /// This allows .Compile(SqlProvider.X) to work without explicit ISqlDialect instance.
    /// </summary>
    /// <param name="provider">The SqlProvider enum value</param>
    /// <param name="dialect">The dialect instance</param>
    /// <exception cref="ArgumentNullException">Thrown if dialect is null</exception>
    public void RegisterDialect(SqlProvider provider, ISqlDialect dialect)
    {
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect), "Dialect cannot be null");

        _lock.EnterWriteLock();
        try
        {
            _dialectsByProvider[provider] = dialect;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a dialect by its SqlProvider enum value.
    /// </summary>
    /// <param name="provider">The SqlProvider enum value</param>
    /// <returns>The registered dialect, or null if not found</returns>
    public ISqlDialect? GetDialect(SqlProvider provider)
    {
        _lock.EnterReadLock();
        try
        {
            return _dialectsByProvider.TryGetValue(provider, out var dialect) ? dialect : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all registered dialects.
    /// </summary>
    /// <returns>Dictionary of all registered dialects by provider</returns>
    public IReadOnlyDictionary<SqlProvider, ISqlDialect> GetAllDialects()
    {
        _lock.EnterReadLock();
        try
        {
            return new Dictionary<SqlProvider, ISqlDialect>(_dialectsByProvider);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all registered dialects and the default.
    /// Useful for testing scenarios.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _defaultDialect = null;
            _dialectsByProvider.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
