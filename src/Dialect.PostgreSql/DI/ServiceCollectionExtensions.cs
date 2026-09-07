namespace Dialect.PostgreSql.DI;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.DI;
using Dialect.Core.Dialects;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// PostgreSQL dialect DI configuration extension methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL dialect with default cache configuration.
    /// Default: 1000 max entries, no TTL, no eviction strategy.
    /// </summary>
    public static IServiceCollection AddPostgreSqlFramework(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var dialect = CreatePostgreSqlDialectFactory();
        
        // Register cache with default config
        services.AddSingleton(new CompiledQueryCache(maxEntries: 1000));
        services.AddSingleton(dialect);
        
        // Register to SqlDialectRegistry
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(SqlProvider.PostgreSql, dialect);
        
        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();
        
        return services;
    }

    /// <summary>
    /// Registers the PostgreSQL dialect with custom cache configuration.
    /// </summary>
    public static IServiceCollection AddPostgreSqlFramework(
        this IServiceCollection services,
        int cacheMaxEntries,
        CacheEvictionStrategy evictionStrategy,
        TimeSpan? cacheTtl = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var dialect = CreatePostgreSqlDialectFactory();
        
        // Register cache with custom config
        services.AddSingleton(new CompiledQueryCache(
            maxEntries: cacheMaxEntries,
            strategy: evictionStrategy,
            ttl: cacheTtl));
        services.AddSingleton(dialect);
        
        // Register to SqlDialectRegistry
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(SqlProvider.PostgreSql, dialect);
        
        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();
        
        return services;
    }

    /// <summary>
    /// Factory method for creating PostgreSQL dialect instance.
    /// </summary>
    internal static ISqlDialect CreatePostgreSqlDialectFactory() => new PostgreSqlDialect();
}
