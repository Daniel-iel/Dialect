namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Service for translating SQL between different database dialects.
/// Supports three translation patterns: default provider, auto-detection, and explicit specification.
/// </summary>
public interface ISqlTranslator
{
    /// <summary>
    /// Translates SQL using the default configured target dialect.
    /// Source provider is auto-detected from the SQL content.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    TranslationResult Translate(string sourceSql);

    /// <summary>
    /// Translates SQL with automatic source provider detection from connection string.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <param name="connectionString">Connection string for detecting source provider.</param>
    /// <param name="targetDialect">The target SQL dialect to compile into.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    TranslationResult Translate(string sourceSql, string connectionString, ISqlDialect targetDialect);

    /// <summary>
    /// Translates SQL with explicitly specified source and target providers.
    /// No provider detection occurs; assumes source provider is correct.
    /// </summary>
    /// <param name="sourceSql">The SQL to translate.</param>
    /// <param name="sourceProvider">The SQL provider of the source SQL.</param>
    /// <param name="targetDialect">The target SQL dialect to compile into.</param>
    /// <returns>Translation result with compiled query or error details.</returns>
    TranslationResult Translate(string sourceSql, SqlProvider sourceProvider, ISqlDialect targetDialect);
}
