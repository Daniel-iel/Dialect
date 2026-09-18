namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Service for translating SQL between different database dialects.
/// Supports multiple translation patterns with increasing levels of automation:
/// - Translate(sql) - full auto-detection with configured default target
/// - Translate(sql, sourceProvider, targetProvider) - explicit SqlProvider enums
/// - Translate(sql, connectionString, targetDialect) - connection string detection
/// - Translate(sql, sourceProvider, targetDialect) - explicit ISqlDialect target
/// </summary>
public interface ISqlTranslator
{
    /// <summary>
    /// Translates SQL using the default configured target dialect.
    /// Source provider is auto-detected from the SQL content via SqlDialectDetector.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no default target dialect is configured</exception>
    TranslationResult Translate(string sourceSql);

    /// <summary>
    /// Translates SQL with explicit source and target providers (SqlProvider enum).
    /// Uses SqlDialectRegistry to resolve dialects from enum values.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <param name="sourceProvider">The SQL provider of the source SQL. If null, auto-detected from SQL syntax.</param>
    /// <param name="targetProvider">The target SQL provider. If null, uses configured default.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    /// <exception cref="ArgumentException">Thrown if a provider cannot be resolved from registry</exception>
    TranslationResult Translate(string sourceSql, SqlProvider? sourceProvider, SqlProvider? targetProvider);

    /// <summary>
    /// Translates SQL with automatic source provider detection from connection string.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <param name="connectionString">Connection string for detecting source provider.</param>
    /// <param name="targetDialect">The target SQL dialect to compile into.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    TranslationResult Translate(string sourceSql, string connectionString, ISqlDialect targetDialect);

    /// <summary>
    /// Translates SQL with explicitly specified source provider and target dialect.
    /// No source provider detection occurs; assumes source provider is correct.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <param name="sourceProvider">The SQL provider of the source SQL.</param>
    /// <param name="targetDialect">The target SQL dialect to compile into.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    TranslationResult Translate(string sourceSql, SqlProvider sourceProvider, ISqlDialect targetDialect);
}
