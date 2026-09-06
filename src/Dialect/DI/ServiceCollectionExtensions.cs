namespace Dialect.Core.DI;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using Dialect.Core.QueryTranslation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the SQL framework in dependency injection containers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQL framework for a specific built-in dialect (SQL Server, PostgreSQL, MySQL).
    /// Also registers the dialect in SqlDialectRegistry as the default and makes it available for .Compile(SqlProvider) overloads.
    /// </summary>
    public static IServiceCollection AddSqlFramework(
        this IServiceCollection services,
        SqlProvider provider)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register the compiled query cache as a singleton (stateless, thread-safe)
        services.AddSingleton(new CompiledQueryCache(maxEntries: 1000));

        // Register the appropriate dialect based on the provider
        var dialect = provider switch
        {
            SqlProvider.SqlServer => CreateSqlServerDialect(),
            SqlProvider.PostgreSql => CreatePostgreSqlDialect(),
            SqlProvider.MySql => CreateMySqlDialect(),
            _ => throw new ArgumentException($"Unknown SQL provider: {provider}", nameof(provider))
        };

        services.AddSingleton(dialect);

        // Register the dialect in the registry as default and by provider
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(provider, dialect);

        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();

        return services;
    }

    /// <summary>
    /// Registers the SQL framework with a custom dialect implementation.
    /// Useful for third-party dialects that are not part of the core package.
    /// Also registers the dialect in SqlDialectRegistry as the default.
    /// </summary>
    public static IServiceCollection AddSqlFramework(
        this IServiceCollection services,
        ISqlDialect dialect)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        // Register the compiled query cache as a singleton
        services.AddSingleton(new CompiledQueryCache(maxEntries: 1000));

        // Register the custom dialect
        services.AddSingleton(dialect);

        // Register the dialect in the registry as default
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);

        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();

        return services;
    }

    /// <summary>
    /// Registers the SQL framework with advanced cache configuration.
    /// Also registers the dialect in SqlDialectRegistry as the default and makes it available for .Compile(SqlProvider) overloads.
    /// </summary>
    public static IServiceCollection AddSqlFramework(
        this IServiceCollection services,
        SqlProvider provider,
        int cacheMaxEntries = 1000,
        CacheEvictionStrategy evictionStrategy = CacheEvictionStrategy.None,
        TimeSpan? cacheTtl = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register the compiled query cache with configuration
        services.AddSingleton(new CompiledQueryCache(cacheMaxEntries, evictionStrategy, cacheTtl));

        // Register the appropriate dialect based on the provider
        var dialect = provider switch
        {
            SqlProvider.SqlServer => CreateSqlServerDialect(),
            SqlProvider.PostgreSql => CreatePostgreSqlDialect(),
            SqlProvider.MySql => CreateMySqlDialect(),
            _ => throw new ArgumentException($"Unknown SQL provider: {provider}", nameof(provider))
        };

        services.AddSingleton(dialect);

        // Register the dialect in the registry as default and by provider
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);
        registry.RegisterDialect(provider, dialect);

        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();

        return services;
    }

    /// <summary>
    /// Registers the SQL framework with a custom dialect and advanced cache configuration.
    /// Also registers the dialect in SqlDialectRegistry as the default.
    /// </summary>
    public static IServiceCollection AddSqlFramework(
        this IServiceCollection services,
        ISqlDialect dialect,
        int cacheMaxEntries = 1000,
        CacheEvictionStrategy evictionStrategy = CacheEvictionStrategy.None,
        TimeSpan? cacheTtl = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        // Register the compiled query cache with configuration
        services.AddSingleton(new CompiledQueryCache(cacheMaxEntries, evictionStrategy, cacheTtl));

        // Register the custom dialect
        services.AddSingleton(dialect);

        // Register the dialect in the registry as default
        var registry = SqlDialectRegistry.Instance;
        registry.SetDefault(dialect);

        // Register ISqlDialectProvider for DI
        services.AddSingleton<ISqlDialectProvider, DefaultSqlDialectProvider>();

        return services;
    }

    /// <summary>
    /// Registers the SQL translation service (ISqlTranslator) with default configuration.
    /// Automatically registers ISqlProviderDetector and parser adapters for all dialects.
    /// </summary>
    public static IServiceCollection AddSqlTranslation(
        this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register the provider detector for connection string analysis
        services.AddSingleton<ISqlProviderDetector, DefaultSqlProviderDetector>();

        // Register parser adapters for each supported SQL dialect
        var parserAdapters = new Dictionary<SqlProvider, SqlParserAdapter>
        {
            { SqlProvider.SqlServer, new SqlServerParserAdapter() },
            { SqlProvider.PostgreSql, new PostgreSqlParserAdapter() },
            { SqlProvider.MySql, new MySqlParserAdapter() }
        };
        services.AddSingleton<IReadOnlyDictionary<SqlProvider, SqlParserAdapter>>(parserAdapters);

        // Register the SQL translator
        services.AddSingleton<ISqlTranslator, DefaultSqlTranslator>();

        return services;
    }

    /// <summary>
    /// Registers the SQL translation service with a default target dialect.
    /// Enables single-argument Translate(sql) calls that automatically target the specified dialect.
    /// </summary>
    public static IServiceCollection AddSqlTranslation(
        this IServiceCollection services,
        ISqlDialect defaultTargetDialect)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (defaultTargetDialect == null)
            throw new ArgumentNullException(nameof(defaultTargetDialect));

        // Register the provider detector
        services.AddSingleton<ISqlProviderDetector, DefaultSqlProviderDetector>();

        // Register parser adapters
        var parserAdapters = new Dictionary<SqlProvider, SqlParserAdapter>
        {
            { SqlProvider.SqlServer, new SqlServerParserAdapter() },
            { SqlProvider.PostgreSql, new PostgreSqlParserAdapter() },
            { SqlProvider.MySql, new MySqlParserAdapter() }
        };
        services.AddSingleton<IReadOnlyDictionary<SqlProvider, SqlParserAdapter>>(parserAdapters);

        // Register translator with default target dialect
        services.AddSingleton<ISqlTranslator>(sp =>
            new DefaultSqlTranslator(
                sp.GetRequiredService<ISqlProviderDetector>(),
                sp.GetRequiredService<IReadOnlyDictionary<SqlProvider, SqlParserAdapter>>(),
                defaultTargetDialect));

        return services;
    }

    /// <summary>
    /// Placeholder for creating SQL Server dialect.
    /// Will be implemented in Dialect.SqlServer package.
    /// </summary>
    private static ISqlDialect CreateSqlServerDialect()
    {
        throw new NotImplementedException(
            "SQL Server dialect is defined in the Dialect.SqlServer package. " +
            "Please reference Dialect.SqlServer and use its factory method instead.");
    }

    /// <summary>
    /// Placeholder for creating PostgreSQL dialect.
    /// Will be implemented in Dialect.PostgreSql package.
    /// </summary>
    private static ISqlDialect CreatePostgreSqlDialect()
    {
        throw new NotImplementedException(
            "PostgreSQL dialect is defined in the Dialect.PostgreSql package. " +
            "Please reference Dialect.PostgreSql and use its factory method instead.");
    }

    /// <summary>
    /// Placeholder for creating MySQL dialect.
    /// Will be implemented in Dialect.MySql package.
    /// </summary>
    private static ISqlDialect CreateMySqlDialect()
    {
        throw new NotImplementedException(
            "MySQL dialect is defined in the Dialect.MySql package. " +
            "Please reference Dialect.MySql and use its factory method instead.");
    }
}
