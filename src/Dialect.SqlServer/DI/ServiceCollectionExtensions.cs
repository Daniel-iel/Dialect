namespace Dialect.SqlServer.DI;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.DI;
using Dialect.Core.Dialects;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// SQL Server dialect DI configuration extension methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQL Server dialect with default cache configuration.
    /// Default: 1000 max entries, no TTL, no eviction strategy.
    /// </summary>
    public static IServiceCollection AddSqlServerFramework(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var dialect = CreateSqlServerDialectFactory();
        
        // Register cache with default config
        services.AddSingleton(new CompiledQueryCache(maxEntries: 1000));
        services.AddSingleton(dialect);
        
        // Register to SqlDialectRegistry
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(SqlProvider.SqlServer, dialect);
        
        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();
        
        return services;
    }

    /// <summary>
    /// Registers the SQL Server dialect with custom cache configuration.
    /// </summary>
    public static IServiceCollection AddSqlServerFramework(
        this IServiceCollection services,
        int cacheMaxEntries,
        CacheEvictionStrategy evictionStrategy,
        TimeSpan? cacheTtl = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var dialect = CreateSqlServerDialectFactory();
        
        // Register cache with custom config
        services.AddSingleton(new CompiledQueryCache(
            maxEntries: cacheMaxEntries,
            strategy: evictionStrategy,
            ttl: cacheTtl));
        services.AddSingleton(dialect);
        
        // Register to SqlDialectRegistry
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(SqlProvider.SqlServer, dialect);
        
        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();
        
        return services;
    }

    /// <summary>
    /// Factory method for creating SQL Server dialect instance.
    /// </summary>
    internal static ISqlDialect CreateSqlServerDialectFactory() => new SqlServerDialect();
}
