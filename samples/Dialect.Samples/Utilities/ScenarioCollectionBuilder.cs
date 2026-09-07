namespace Dialect.Samples.Utilities;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Fluent builder for collecting scenario definitions.
/// Provides a clean API for adding scenarios declaratively.
/// 
/// Usage:
/// <code>
/// var scenarios = new ScenarioCollectionBuilder()
///     .Add("Simple SELECT", d => SqlBuilder.Select("*").From("Users").Build().Compile(d))
///     .Add("SELECT with WHERE", d => SqlBuilder.Select("*").From("Users")
///         .Where("UserId", 1).Build().Compile(d))
///     .Build();
/// </code>
/// </summary>
public class ScenarioCollectionBuilder
{
    private readonly List<ScenarioDefinition> _scenarios = new();

    /// <summary>
    /// Create a builder for scenario definitions.
    /// </summary>
    public ScenarioCollectionBuilder()
    {
    }

    /// <summary>
    /// Add a simple scenario with name and builder function.
    /// </summary>
    /// <param name="name">Display name for the scenario</param>
    /// <param name="builder">Function that builds the compiled query for a dialect</param>
    public ScenarioCollectionBuilder Add(string name, Func<ISqlDialect, CompiledQuery> builder)
    {
        return Add(new ScenarioDefinition(name, builder));
    }

    /// <summary>
    /// Add a scenario with name, description, and builder function.
    /// </summary>
    /// <param name="name">Display name</param>
    /// <param name="description">Detailed description of what scenario demonstrates</param>
    /// <param name="builder">Builder function</param>
    public ScenarioCollectionBuilder Add(
        string name,
        string description,
        Func<ISqlDialect, CompiledQuery> builder)
    {
        return Add(new ScenarioDefinition(name, description, builder));
    }

    /// <summary>
    /// Add a scenario with tags for categorization.
    /// </summary>
    /// <param name="name">Display name</param>
    /// <param name="builder">Builder function</param>
    /// <param name="tags">Category tags (e.g., "SELECT", "WHERE", "Performance")</param>
    public ScenarioCollectionBuilder Add(
        string name,
        Func<ISqlDialect, CompiledQuery> builder,
        params string[] tags)
    {
        return Add(new ScenarioDefinition(name, "", builder, tags));
    }

    /// <summary>
    /// Add a full scenario definition.
    /// </summary>
    public ScenarioCollectionBuilder Add(ScenarioDefinition scenario)
    {
        if (scenario == null)
            throw new ArgumentNullException(nameof(scenario));

        _scenarios.Add(scenario);
        return this;
    }

    /// <summary>
    /// Finalize scenario collection and execute all scenarios.
    /// Returns the list of compiled results for all scenarios across all dialects.
    /// </summary>
    public List<ScenarioExecutionResult> Build()
    {
        var results = new List<ScenarioExecutionResult>();
        var dialectHelper = new DialectHelper();

        foreach (var scenario in _scenarios)
        {
            var compiledResults = new Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)>();

            // Compile for all dialects
            foreach (var (dialectName, dialect) in dialectHelper.GetAllDialects())
            {
                try
                {
                    var compiled = scenario.Builder(dialect);
                    compiledResults[dialectName] = (compiled.Sql, compiled.Parameters);
                }
                catch (Exception ex)
                {
                    compiledResults[dialectName] = ($"ERROR: {ex.Message}", new Dictionary<string, object?>());
                }
            }

            results.Add(new ScenarioExecutionResult(scenario.Name, compiledResults, new Dictionary<string, (List<dynamic>, long, int, string?)>()));
        }

        return results;
    }

    /// <summary>
    /// Get scenario count.
    /// </summary>
    public int Count => _scenarios.Count;

    /// <summary>
    /// Get a scenario by name.
    /// </summary>
    public ScenarioDefinition? GetByName(string name) =>
        _scenarios.FirstOrDefault(s => s.Name == name);
}

