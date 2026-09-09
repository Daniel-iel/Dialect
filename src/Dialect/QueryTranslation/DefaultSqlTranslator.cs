namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.DI;

/// <summary>
/// Default implementation of ISqlTranslator.
/// Orchestrates parsing, untranslatable detection, and compilation.
/// Stateless and thread-safe (singleton pattern).
/// </summary>
public sealed class DefaultSqlTranslator : ISqlTranslator
{
    private readonly ISqlProviderDetector _providerDetector;
    private readonly ISqlDialect? _defaultTargetDialect;
    private readonly IReadOnlyDictionary<SqlProvider, SqlParserAdapter> _parserAdapters;

    /// <summary>
    /// Constructor without default dialect (requires explicit dialect for each translation).
    /// </summary>
    public DefaultSqlTranslator(
        ISqlProviderDetector providerDetector,
        IReadOnlyDictionary<SqlProvider, SqlParserAdapter> parserAdapters)
    {
        _providerDetector = providerDetector ?? throw new ArgumentNullException(nameof(providerDetector));
        _parserAdapters = parserAdapters ?? throw new ArgumentNullException(nameof(parserAdapters));
        _defaultTargetDialect = null;
    }

    /// <summary>
    /// Constructor with default target dialect.
    /// </summary>
    public DefaultSqlTranslator(
        ISqlProviderDetector providerDetector,
        IReadOnlyDictionary<SqlProvider, SqlParserAdapter> parserAdapters,
        ISqlDialect defaultTargetDialect)
        : this(providerDetector, parserAdapters)
    {
        _defaultTargetDialect = defaultTargetDialect ?? throw new ArgumentNullException(nameof(defaultTargetDialect));
    }

    /// <summary>
    /// Translates SQL using the default configured target dialect and auto-detected source provider.
    /// </summary>
    public TranslationResult Translate(string sourceSql)
    {
        if (_defaultTargetDialect is null)
        {
            return new TranslationResult
            {
                ErrorMessage = "No default target dialect configured. Use Translate(sql, connStr, targetDialect) instead."
            };
        }

        var detectedProvider = DetectSourceProvider(sourceSql);
        if (detectedProvider is null)
        {
            return new TranslationResult
            {
                ErrorMessage = "Could not detect source SQL provider. Specify provider explicitly."
            };
        }

        return TranslateInternal(sourceSql, detectedProvider.Value, _defaultTargetDialect);
    }

    /// <summary>
    /// Translates SQL with source provider auto-detection from connection string.
    /// </summary>
    public TranslationResult Translate(string sourceSql, string connectionString, ISqlDialect targetDialect)
    {
        if (string.IsNullOrWhiteSpace(sourceSql))
        {
            return new TranslationResult
            {
                ErrorMessage = "Source SQL cannot be empty."
            };
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new TranslationResult
            {
                ErrorMessage = "Connection string cannot be empty."
            };
        }

        var detectedProvider = _providerDetector.Detect(connectionString);
        if (detectedProvider is null)
        {
            return new TranslationResult
            {
                ErrorMessage = "Could not detect source SQL provider from connection string."
            };
        }

        return TranslateInternal(sourceSql, detectedProvider.Value, targetDialect);
    }

    /// <summary>
    /// Translates SQL with explicitly specified source provider.
    /// </summary>
    public TranslationResult Translate(string sourceSql, SqlProvider sourceProvider, ISqlDialect targetDialect)
    {
        if (string.IsNullOrWhiteSpace(sourceSql))
        {
            return new TranslationResult
            {
                ErrorMessage = "Source SQL cannot be empty."
            };
        }

        return TranslateInternal(sourceSql, sourceProvider, targetDialect);
    }

    /// <summary>
    /// Translates SQL with optional source and target SqlProvider enums.
    /// Source is auto-detected if not specified; target uses default if not specified.
    /// </summary>
    public TranslationResult Translate(string sourceSql, SqlProvider? sourceProvider, SqlProvider? targetProvider)
    {
        if (string.IsNullOrWhiteSpace(sourceSql))
        {
            return new TranslationResult
            {
                ErrorMessage = "Source SQL cannot be empty."
            };
        }

        // Resolve source provider
        SqlProvider resolvedSource;
        if (sourceProvider.HasValue)
        {
            resolvedSource = sourceProvider.Value;
        }
        else
        {
            var detectedSource = DetectSourceProvider(sourceSql);
            if (detectedSource is null)
            {
                return new TranslationResult
                {
                    ErrorMessage = "Could not auto-detect source SQL provider. Specify provider explicitly."
                };
            }
            resolvedSource = detectedSource.Value;
        }

        // Resolve target dialect
        ISqlDialect targetDialect;
        if (targetProvider.HasValue)
        {
            var dialect = SqlDialectRegistry.Instance.GetDialect(targetProvider.Value);
            if (dialect is null)
            {
                return new TranslationResult
                {
                    DetectedSourceProvider = resolvedSource,
                    ErrorMessage = $"No dialect registered for target provider: {targetProvider}. " +
                                   $"Ensure AddSqlFramework({targetProvider}) has been called."
                };
            }
            targetDialect = dialect;
        }
        else
        {
            var defaultDialect = SqlDialectRegistry.Instance.GetDefault();
            if (defaultDialect is null)
            {
                return new TranslationResult
                {
                    DetectedSourceProvider = resolvedSource,
                    ErrorMessage = "No default target dialect configured. " +
                                   "Call AddSqlFramework() in your Program.cs or specify target provider explicitly."
                };
            }
            targetDialect = defaultDialect;
        }

        return TranslateInternal(sourceSql, resolvedSource, targetDialect);
    }

    /// <summary>
    /// Internal translation logic orchestrating all phases.
    /// 1. Parse source SQL to AST using dialect-specific parser adapter
    /// 2. Detect untranslatable constructs
    /// 3. Compile AST to target SQL using target dialect renderer
    /// </summary>
    private TranslationResult TranslateInternal(
        string sourceSql,
        SqlProvider sourceProvider,
        ISqlDialect targetDialect)
    {
        // Get parser adapter for source dialect
        if (!_parserAdapters.TryGetValue(sourceProvider, out var parserAdapter))
        {
            return new TranslationResult
            {
                DetectedSourceProvider = sourceProvider,
                ErrorMessage = $"No parser adapter available for {sourceProvider}."
            };
        }

        // Phase 1: Detect untranslatable constructs
        var untranslatableConstructs = parserAdapter.DetectUntranslatableConstructs(sourceSql);

        // Phase 2: Attempt to parse to AST
        var ast = parserAdapter.ParseToAst(sourceSql);
        if (ast is null)
        {
            return new TranslationResult
            {
                DetectedSourceProvider = sourceProvider,
                UntranslatableConstructs = untranslatableConstructs,
                ErrorMessage = $"Failed to parse {sourceProvider} SQL. Parser not yet implemented."
            };
        }

        // Phase 3: Compile AST to target SQL
        var compiledQuery = ast.Compile(targetDialect);

        return new TranslationResult
        {
            Compiled = compiledQuery,
            DetectedSourceProvider = sourceProvider,
            UntranslatableConstructs = untranslatableConstructs
        };
    }

    /// <summary>
    /// Detects source SQL provider from the SQL content using pattern analysis.
    /// Uses SqlDialectDetector to identify TSQL, PostgreSQL, MySQL syntax patterns.
    /// </summary>
    private static SqlProvider? DetectSourceProvider(string sql)
    {
        return SqlDialectDetector.DetectDialectProvider(sql);
    }
}
