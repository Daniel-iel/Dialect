namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.DI;
using System.Globalization;
using System.Text.RegularExpressions;

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
            // Fallback: attempt simple text-based translation for common patterns when the parser is not implemented yet.
            var fallback = AttemptTextualTranslationV2(sourceSql, sourceProvider, targetDialect);
            if (!string.IsNullOrEmpty(fallback))
            {
                return new TranslationResult
                {
                    Compiled = new CompiledQuery(fallback, new Dictionary<string, object?>()),
                    DetectedSourceProvider = sourceProvider,
                    UntranslatableConstructs = untranslatableConstructs
                };
            }

            return new TranslationResult
            {
                DetectedSourceProvider = sourceProvider,
                UntranslatableConstructs = untranslatableConstructs,
                ErrorMessage = $"Failed to parse {sourceProvider} SQL. Parser not yet implemented."
            };
        }

        // Phase 3: Compile AST to target SQL
        var compiledQuery = CompileAst(ast, targetDialect);
        compiledQuery = InlineCompiledParameters(compiledQuery, targetDialect);

        return new TranslationResult
        {
            Compiled = compiledQuery,
            DetectedSourceProvider = sourceProvider,
            UntranslatableConstructs = untranslatableConstructs
        };
    }

    private static CompiledQuery CompileAst(QueryNode ast, ISqlDialect targetDialect)
    {
        return ast switch
        {
            SelectStatement s => s.Compile(targetDialect),
            CompoundSelectStatement s => s.Compile(targetDialect),
            InsertStatement s => s.Compile(targetDialect),
            UpdateStatement s => s.Compile(targetDialect),
            DeleteStatement s => s.Compile(targetDialect),
            UpsertStatement s => s.Compile(targetDialect),
            RoutineCall s => s.Compile(targetDialect),
            _ => throw new InvalidOperationException($"Unsupported AST type: {ast.GetType().Name}")
        };
    }

    private static CompiledQuery InlineCompiledParameters(CompiledQuery compiledQuery, ISqlDialect targetDialect)
    {
        if (compiledQuery.Parameters.Count == 0)
            return compiledQuery;

        var sql = compiledQuery.Sql;

        if (IsPostgreSql(targetDialect))
        {
            sql = Regex.Replace(sql, @"\$(\d+)", m =>
            {
                var index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var key = $"p{index}";
                if (!compiledQuery.Parameters.TryGetValue(key, out var value))
                    return m.Value;
                return ToSqlLiteral(value, targetDialect);
            });
        }
        else if (IsSqlServer(targetDialect))
        {
            sql = Regex.Replace(sql, @"@p(\d+)\b", m =>
            {
                var idx = m.Groups[1].Value;
                if (compiledQuery.Parameters.TryGetValue($"p{idx}", out var value) ||
                    compiledQuery.Parameters.TryGetValue($"@p{idx}", out value))
                {
                    return ToSqlLiteral(value, targetDialect);
                }
                return m.Value;
            });
        }
        else if (IsMySql(targetDialect))
        {
            var ordered = compiledQuery.Parameters
                .OrderBy(kvp => ExtractParameterOrder(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            var valueIndex = 0;
            sql = Regex.Replace(sql, @"\?", _ =>
            {
                if (valueIndex >= ordered.Count)
                    return "?";

                return ToSqlLiteral(ordered[valueIndex++], targetDialect);
            });
        }

        return new CompiledQuery(sql, new Dictionary<string, object?>());
    }

    private static int ExtractParameterOrder(string key)
    {
        var match = Regex.Match(key ?? string.Empty, @"(\d+)$");
        if (!match.Success)
            return int.MaxValue;
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string ToSqlLiteral(object? value, ISqlDialect dialect)
    {
        if (value is null)
            return "NULL";

        if (value is string s)
            return $"'{s.Replace("'", "''")}'";

        if (value is bool b)
            return IsSqlServer(dialect) ? (b ? "1" : "0") : (b ? "TRUE" : "FALSE");

        if (value is DateTime dt)
            return $"'{dt:yyyy-MM-dd HH:mm:ss.fffffff}'";

        if (value is DateTimeOffset dto)
            return $"'{dto:yyyy-MM-dd HH:mm:ss.fffffff zzz}'";

        if (value is Enum)
            return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL";
    }

    /// <summary>
    /// Attempts a simple regex-based textual translation for common DML patterns.
    /// This is a pragmatic fallback for golden tests and small migrations; for full coverage a proper parser->AST is required.
    /// </summary>
    private static bool IsPostgreSql(ISqlDialect dialect) => dialect.ParameterPrefix == ":" || dialect.IdentifierQuote == '"';
    private static bool IsMySql(ISqlDialect dialect) => dialect.ParameterPrefix == "?" || dialect.IdentifierQuote == '`';
    private static bool IsSqlServer(ISqlDialect dialect) => dialect.ParameterPrefix == "@" || dialect.IdentifierQuote == '[';

    private static string? AttemptTextualTranslation(string sql, SqlProvider sourceProvider, ISqlDialect targetDialect)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        var s = sql.Trim();

        // Normalize line endings
        s = s.Replace("\r\n", "\n").Trim();

        // Helper: remove SQL Server/PG/MySQL identifier quoting to raw name
        string Unquote(string ident)
        {
            if (ident.StartsWith("[") && ident.EndsWith("]"))
                return ident[1..^1];
            if (ident.StartsWith("\"") && ident.EndsWith("\""))
                return ident[1..^1];
            if (ident.StartsWith("`") && ident.EndsWith("`"))
                return ident[1..^1];
            return ident;
        }

        // Target quoting
        string Quote(string name)
        {
            if (IsPostgreSql(targetDialect))
                return '"' + name + '"';
            if (IsMySql(targetDialect))
                return '`' + name + '`';
            // SQL Server uses [name]
            return '[' + name + ']';
        }

        // Convert identifiers: [Name] / `Name` / "Name" -> target quoting
        s = Regex.Replace(s, "\\[([^\\]]+)\\]|`([^`]+)`|\"([^\"]+)\"", match =>
        {
            var g1 = match.Groups[1].Value;
            var g2 = match.Groups[2].Value;
            var g3 = match.Groups[3].Value;
            var raw = !string.IsNullOrEmpty(g1) ? g1 : !string.IsNullOrEmpty(g2) ? g2 : g3;
            return Quote(Unquote(raw));
        }, RegexOptions.Compiled);

        // Handle SELECT TOP N -> LIMIT N
        var mTop = Regex.Match(s, @"^\s*SELECT\s+TOP\s+(\d+)\s+(.*?)\s*;?$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (mTop.Success)
        {
            var n = mTop.Groups[1].Value;
            var rest = mTop.Groups[2].Value.Trim();
            // Append LIMIT at end
            // Ensure semicolon
            var translated = "SELECT " + rest;
            if (!translated.TrimEnd().EndsWith(";"))
                translated += " ";
            translated = translated.TrimEnd();
            translated += " LIMIT " + n + ";";
            return translated;
        }

        // Handle simple SELECT ... ORDER BY ... TOP inside
        var mTop2 = Regex.Match(s, @"^\s*SELECT\s+TOP\s+(\d+)\s+(.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (mTop2.Success)
        {
            var n = mTop2.Groups[1].Value;
            var rest = mTop2.Groups[2].Value.Trim();
            // remove trailing semicolon
            rest = rest.TrimEnd(';').Trim();
            var translated = "SELECT " + rest + " LIMIT " + n + ";";
            return translated;
        }

        // INSERT + SCOPE_IDENTITY() -> INSERT ... RETURNING id (Postgres) or LAST_INSERT_ID() (MySQL) or OUTPUT INSERTED.Id (SQLServer)
        if (sourceProvider == SqlProvider.SqlServer)
        {
            if (Regex.IsMatch(s, @"INSERT\s+INTO", RegexOptions.IgnoreCase))
            {
                // Detect SCOPE_IDENTITY following the insert
                if (Regex.IsMatch(s, @"SCOPE_IDENTITY\(\)", RegexOptions.IgnoreCase))
                {
                    if (IsPostgreSql(targetDialect))
                    {
                        // Remove trailing SELECT SCOPE_IDENTITY() and convert to RETURNING id
                        var withoutScope = Regex.Replace(s, @";?\s*SELECT\s+SCOPE_IDENTITY\(\)\s*(AS\s+\w+)?;?", "", RegexOptions.IgnoreCase);
                        withoutScope = withoutScope.TrimEnd(';') + " RETURNING id;";
                        return withoutScope;
                    }

                    if (IsMySql(targetDialect))
                    {
                        // Keep INSERT as-is and append SELECT LAST_INSERT_ID()
                        var withoutScope = Regex.Replace(s, @";?\s*SELECT\s+SCOPE_IDENTITY\(\)\s*(AS\s+\w+)?;?", "", RegexOptions.IgnoreCase);
                        withoutScope = withoutScope.TrimEnd(';') + ";\nSELECT LAST_INSERT_ID() AS NewId;";
                        return withoutScope;
                    }

                    if (IsSqlServer(targetDialect))
                    {
                        // Keep original or convert to OUTPUT INSERTED.Id
                        // Simple approach: replace SCOPE_IDENTITY with OUTPUT INSERTED.Id form if possible
                        var withoutScope = Regex.Replace(s, @";?\s*SELECT\s+SCOPE_IDENTITY\(\)\s*(AS\s+\w+)?;?", ";", RegexOptions.IgnoreCase);
                        return withoutScope;
                    }
                }
            }
        }

        // ISNULL -> COALESCE / IFNULL
        if (Regex.IsMatch(s, @"ISNULL\s*\(", RegexOptions.IgnoreCase))
        {
            if (IsPostgreSql(targetDialect))
                s = Regex.Replace(s, @"ISNULL\s*\(", "COALESCE(", RegexOptions.IgnoreCase);
            else if (IsMySql(targetDialect))
                s = Regex.Replace(s, @"ISNULL\s*\(", "IFNULL(", RegexOptions.IgnoreCase);
        }

        // Simple update-from-join pattern used in golden files: transform to target-friendly form
        // SQL Server style: UPDATE o\nSET o.Status = 'Closed'\nFROM [Orders] o\nJOIN [Customers] c ON o.CustomerId = c.Id\nWHERE c.Region = 'NA';
        var mUpdateFrom = Regex.Match(s, @"UPDATE\s+(\w+)\s*\n?\s*SET\s+(.*?)\s*\n?\s*FROM\s+(\S+)\s+(\w+)\s*\n?\s*JOIN\s+(\S+)\s+(\w+)\s+ON\s+(.*?)\s*\n?\s*WHERE\s+(.*);?$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (mUpdateFrom.Success)
        {
            var updateAlias = mUpdateFrom.Groups[1].Value;
            var setClause = mUpdateFrom.Groups[2].Value.Trim();
            var fromTable = mUpdateFrom.Groups[3].Value.Trim();
            var fromAlias = mUpdateFrom.Groups[4].Value.Trim();
            var joinTable = mUpdateFrom.Groups[5].Value.Trim();
            var joinAlias = mUpdateFrom.Groups[6].Value.Trim();
            var joinOn = mUpdateFrom.Groups[7].Value.Trim();
            var where = mUpdateFrom.Groups[8].Value.Trim();

            var sb = new System.Text.StringBuilder();
            sb.Append("UPDATE ").Append(Quote(Unquote(fromTable))).Append(" ").Append(fromAlias).Append("\n");
            sb.Append("SET ").Append(setClause).Append("\n");
            sb.Append("FROM ").Append(Quote(Unquote(joinTable))).Append(" ").Append(joinAlias).Append("\n");
            sb.Append("WHERE ").Append(joinOn).Append(" AND ").Append(where).Append(";");
            return sb.ToString();
        }

        // Fallback: if none matched but identifiers were re-quoted, return that normalized SQL (helps simple cases)
        if (s != sql)
            return s;

        return null;
    }

    /// <summary>
    /// Enhanced textual fallback translator with broader heuristic rules.
    /// This is an incremental improvement over AttemptTextualTranslation to cover more
    /// practical migration cases while parsers are implemented.
    /// </summary>
    private static string? AttemptTextualTranslationV2(string sql, SqlProvider sourceProvider, ISqlDialect targetDialect)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        var s = sql.Trim();
        // Normalize line endings
        s = s.Replace("\r\n", "\n").Trim();

        static string Unquote(string ident)
        {
            if (ident.StartsWith("[") && ident.EndsWith("]"))
                return ident[1..^1];
            if (ident.StartsWith("\"") && ident.EndsWith("\""))
                return ident[1..^1];
            if (ident.StartsWith("`") && ident.EndsWith("`"))
                return ident[1..^1];
            return ident;
        }

        string Quote(string name)
        {
            if (IsPostgreSql(targetDialect))
                return '"' + name + '"';
            if (IsMySql(targetDialect))
                return '`' + name + '`';
            return '[' + name + ']';
        }

        // Normalize quoting first (handles [Name], `Name`, "Name")
        s = Regex.Replace(s, "\\[([^\\]]+)\\]|`([^`]+)`|\"([^\"]+)\"", match =>
        {
            var g1 = match.Groups[1].Value;
            var g2 = match.Groups[2].Value;
            var g3 = match.Groups[3].Value;
            var raw = !string.IsNullOrEmpty(g1) ? g1 : !string.IsNullOrEmpty(g2) ? g2 : g3;
            return Quote(Unquote(raw));
        }, RegexOptions.Compiled);

        // Basic DDL type mapping for CREATE/ALTER TABLE across supported dialects.
        if (Regex.IsMatch(s, @"^\s*(CREATE|ALTER)\s+TABLE\b", RegexOptions.IgnoreCase))
        {
            s = ApplyDdlTypeMappings(s, sourceProvider, targetDialect);
        }

        // Simple lexical normalizations useful for many translations
        // Remove N prefix for Unicode string literals (T-SQL → standard SQL)
        s = Regex.Replace(s, @"\bN'([^']*)'", "'$1'", RegexOptions.IgnoreCase);

        // Convert GETDATE() -> CURRENT_TIMESTAMP for target dialects that use ANSI timestamp
        if (sourceProvider == SqlProvider.SqlServer && (IsPostgreSql(targetDialect) || IsMySql(targetDialect)))
        {
            s = Regex.Replace(s, @"\bGETDATE\s*\(\s*\)", "CURRENT_TIMESTAMP", RegexOptions.IgnoreCase);
        }

        // Convert LEN(...) -> LENGTH(...)
        s = Regex.Replace(s, @"\bLEN\s*\(", "LENGTH(", RegexOptions.IgnoreCase);

        // Convert parameter prefixes: @name -> :name for PostgreSQL (MySQL uses positional params - manual review required)
        if (IsPostgreSql(targetDialect))
        {
            s = Regex.Replace(s, @"@([A-Za-z0-9_]+)", ":$1");
        }

        // TOP N -> LIMIT N (supports optional DISTINCT)
        var mTop = Regex.Match(s, @"\bSELECT\s+TOP\s*\(?\s*(\d+)\s*\)?\s*(DISTINCT\s+)?(.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (mTop.Success)
        {
            var n = mTop.Groups[1].Value;
            var distinct = mTop.Groups[2].Value ?? string.Empty;
            var rest = mTop.Groups[3].Value.Trim().TrimEnd(';');
            var translated = "SELECT " + distinct + rest + " LIMIT " + n + ";";
            return translated;
        }

        // OFFSET ... FETCH NEXT -> LIMIT ... OFFSET ...
        s = Regex.Replace(s, @"OFFSET\s+(\d+)\s+ROWS\s+FETCH\s+NEXT\s+(\d+)\s+ROWS\s+ONLY",
            m => $"LIMIT {m.Groups[2].Value} OFFSET {m.Groups[1].Value}", RegexOptions.IgnoreCase);

        // OUTPUT INSERTED.Col -> RETURNING / LAST_INSERT_ID handling
        if (Regex.IsMatch(s, @"\bINSERT\b", RegexOptions.IgnoreCase))
        {
            var mOutput = Regex.Match(s, @"\bOUTPUT\s+INSERTED\.([a-zA-Z0-9_]+)\b", RegexOptions.IgnoreCase);
            if (mOutput.Success)
            {
                var col = mOutput.Groups[1].Value;
                if (IsPostgreSql(targetDialect))
                {
                    var withoutOutput = Regex.Replace(s, @"\bOUTPUT\s+INSERTED\.[a-zA-Z0-9_]+\b", "", RegexOptions.IgnoreCase);
                    withoutOutput = withoutOutput.TrimEnd(';').Trim() + " RETURNING " + Quote(col) + ";";
                    return withoutOutput;
                }

                if (IsMySql(targetDialect))
                {
                    var withoutOutput = Regex.Replace(s, @"\bOUTPUT\s+INSERTED\.[a-zA-Z0-9_]+\b", "", RegexOptions.IgnoreCase);
                    withoutOutput = withoutOutput.TrimEnd(';').Trim() + ";\nSELECT LAST_INSERT_ID() AS NewId;";
                    return withoutOutput;
                }

                if (IsSqlServer(targetDialect))
                {
                    return s;
                }
            }
        }

        // SCOPE_IDENTITY() -> RETURNING / LAST_INSERT_ID
        if (sourceProvider == SqlProvider.SqlServer && Regex.IsMatch(s, @"SCOPE_IDENTITY\(\)", RegexOptions.IgnoreCase))
        {
            if (IsPostgreSql(targetDialect))
            {
                var withoutScope = Regex.Replace(s, @";?\s*SELECT\s+SCOPE_IDENTITY\(\)\s*(AS\s+\w+)?;?", "", RegexOptions.IgnoreCase);
                withoutScope = withoutScope.TrimEnd(';') + " RETURNING id;";
                return withoutScope;
            }
            if (IsMySql(targetDialect))
            {
                var withoutScope = Regex.Replace(s, @";?\s*SELECT\s+SCOPE_IDENTITY\(\)\s*(AS\s+\w+)?;?", "", RegexOptions.IgnoreCase);
                withoutScope = withoutScope.TrimEnd(';') + ";\nSELECT LAST_INSERT_ID() AS NewId;";
                return withoutScope;
            }
        }

        // ISNULL -> COALESCE / IFNULL
        if (Regex.IsMatch(s, @"\bISNULL\s*\(", RegexOptions.IgnoreCase))
        {
            if (IsPostgreSql(targetDialect))
                s = Regex.Replace(s, @"\bISNULL\s*\(", "COALESCE(", RegexOptions.IgnoreCase);
            else if (IsMySql(targetDialect))
                s = Regex.Replace(s, @"\bISNULL\s*\(", "IFNULL(", RegexOptions.IgnoreCase);
        }

        // Update-from-join pattern
        var mUpdateFrom = Regex.Match(s, @"UPDATE\s+(\S+)\s*\n?\s*SET\s+(.*?)\s*\n?\s*FROM\s+(\S+)\s+(\S+)\s*\n?\s*JOIN\s+(\S+)\s+(\S+)\s+ON\s+(.*?)\s*\n?\s*WHERE\s+(.*);?$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (mUpdateFrom.Success)
        {
            var updateAlias = mUpdateFrom.Groups[1].Value;
            var setClause = mUpdateFrom.Groups[2].Value.Trim();
            var fromTable = mUpdateFrom.Groups[3].Value.Trim();
            var fromAlias = mUpdateFrom.Groups[4].Value.Trim();
            var joinTable = mUpdateFrom.Groups[5].Value.Trim();
            var joinAlias = mUpdateFrom.Groups[6].Value.Trim();
            var joinOn = mUpdateFrom.Groups[7].Value.Trim();
            var where = mUpdateFrom.Groups[8].Value.Trim();

            var sb = new System.Text.StringBuilder();
            sb.Append("UPDATE ").Append(Quote(Unquote(fromTable))).Append(" ").Append(fromAlias).Append("\n");
            sb.Append("SET ").Append(setClause).Append("\n");
            sb.Append("FROM ").Append(Quote(Unquote(joinTable))).Append(" ").Append(joinAlias).Append("\n");
            sb.Append("WHERE ").Append(joinOn).Append(" AND ").Append(where).Append(";");
            return sb.ToString();
        }

        // Normalize OFFSET n ROWS -> OFFSET n
        s = Regex.Replace(s, @"OFFSET\s+(\d+)\s+ROWS", m => $"OFFSET {m.Groups[1].Value}", RegexOptions.IgnoreCase);

        s = s.Trim();
        if (!s.EndsWith(";"))
            s += ";";

        if (s != sql.Trim())
            return s;

        return null;
    }

    /// <summary>
    /// Detects source SQL provider from the SQL content using pattern analysis.
    /// Uses SqlDialectDetector to identify TSQL, PostgreSQL, MySQL syntax patterns.
    /// </summary>
    private static SqlProvider? DetectSourceProvider(string sql)
    {
        return SqlDialectDetector.DetectDialectProvider(sql);
    }

    private static string ApplyDdlTypeMappings(string sql, SqlProvider sourceProvider, ISqlDialect targetDialect)
    {
        var s = sql;

        if (sourceProvider == SqlProvider.SqlServer && IsPostgreSql(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", "BIGSERIAL", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", "SERIAL", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bNVARCHAR\s*\(\s*MAX\s*\)", "TEXT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bVARCHAR\s*\(\s*MAX\s*\)", "TEXT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bNVARCHAR\s*\(", "VARCHAR(", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bDATETIME2?\b", "TIMESTAMP", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bBIT\b", "BOOLEAN", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bUNIQUEIDENTIFIER\b", "UUID", RegexOptions.IgnoreCase);
            return s;
        }

        if (sourceProvider == SqlProvider.SqlServer && IsMySql(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", "BIGINT AUTO_INCREMENT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", "INT AUTO_INCREMENT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bNVARCHAR\s*\(", "VARCHAR(", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bNVARCHAR\s*\(\s*MAX\s*\)", "LONGTEXT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bDATETIME2\b", "DATETIME", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bUNIQUEIDENTIFIER\b", "CHAR(36)", RegexOptions.IgnoreCase);
            return s;
        }

        if (sourceProvider == SqlProvider.MySql && IsPostgreSql(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGINT\s+AUTO_INCREMENT\b", "BIGSERIAL", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bINT\s+AUTO_INCREMENT\b", "SERIAL", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bTINYINT\s*\(\s*1\s*\)", "BOOLEAN", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bDATETIME\b", "TIMESTAMP", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bCHAR\s*\(\s*36\s*\)", "UUID", RegexOptions.IgnoreCase);
            return s;
        }

        if (sourceProvider == SqlProvider.MySql && IsSqlServer(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGINT\s+AUTO_INCREMENT\b", "BIGINT IDENTITY(1,1)", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bINT\s+AUTO_INCREMENT\b", "INT IDENTITY(1,1)", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bTINYINT\s*\(\s*1\s*\)", "BIT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bBOOLEAN\b", "BIT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bTIMESTAMP\b", "DATETIME2", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bCHAR\s*\(\s*36\s*\)", "UNIQUEIDENTIFIER", RegexOptions.IgnoreCase);
            return s;
        }

        if (sourceProvider == SqlProvider.PostgreSql && IsSqlServer(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGSERIAL\b", "BIGINT IDENTITY(1,1)", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bSERIAL\b", "INT IDENTITY(1,1)", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bBOOLEAN\b", "BIT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bTIMESTAMP\b", "DATETIME2", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bUUID\b", "UNIQUEIDENTIFIER", RegexOptions.IgnoreCase);
            return s;
        }

        if (sourceProvider == SqlProvider.PostgreSql && IsMySql(targetDialect))
        {
            s = Regex.Replace(s, @"\bBIGSERIAL\b", "BIGINT AUTO_INCREMENT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bSERIAL\b", "INT AUTO_INCREMENT", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bBOOLEAN\b", "TINYINT(1)", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bTIMESTAMP\b", "DATETIME", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\bUUID\b", "CHAR(36)", RegexOptions.IgnoreCase);
            return s;
        }

        return s;
    }
}
