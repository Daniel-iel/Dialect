namespace Dialect.Samples.Utilities;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Immutable definition of a database query scenario.
///
/// Enables data-driven example design by separating scenario metadata from execution logic.
/// Instead of defining scenarios as private methods, scenarios are declared as static data
/// using ScenarioDefinition records, which can be:
/// - Queried and filtered dynamically
/// - Tested independently
/// - Reused across multiple example classes
/// - Serialized or stored for testing frameworks
///
/// Example usage:
/// <code>
/// private static readonly ScenarioDefinition[] Scenarios = new[]
/// {
///     new ScenarioDefinition(
///         "Simple SELECT",
///         "Basic SELECT query retrieving all rows",
///         dialect => SqlBuilder.Select("*").From("Users").Build().Compile(dialect)
///     ),
///     new ScenarioDefinition(
///         "Filtered SELECT",
///         "SELECT with WHERE clause",
///         dialect => SqlBuilder.Select("*").From("Users").Where("Age", 21).Build().Compile(dialect),
///         new[] { "filter", "where" }
///     ),
/// };
/// </code>
/// </summary>
public record ScenarioDefinition(
    /// <summary>Display name for the scenario (e.g., "Simple SELECT *")</summary>
    string Name,

    /// <summary>Detailed description of what this scenario demonstrates</summary>
    string Description,

    /// <summary>
    /// Delegate that builds the compiled query for a given SQL dialect.
    /// Returns the compiled SQL statement with parameters.
    /// </summary>
    Func<ISqlDialect, CompiledQuery> Builder,

    /// <summary>
    /// Category tags for filtering scenarios (e.g., "SELECT", "JOIN", "Performance", "Advanced")
    /// </summary>
    string[] Tags = null
)
{
    /// <summary>
    /// Create a scenario with name, description, and builder.
    /// </summary>
    public ScenarioDefinition(string name, string description, Func<ISqlDialect, CompiledQuery> builder)
        : this(name, description, builder, Array.Empty<string>())
    {
    }
};