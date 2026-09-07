namespace Dialect.Samples.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Repository interface for querying and managing scenario definitions.
/// Provides abstraction for scenario storage and retrieval patterns.
/// </summary>
public interface IScenarioRepository
{
    /// <summary>
    /// Gets all scenarios in the repository.
    /// </summary>
    /// <returns>Enumerable of all available scenarios</returns>
    IEnumerable<ScenarioDefinition> GetAll();

    /// <summary>
    /// Gets a scenario by its exact name.
    /// </summary>
    /// <param name="name">The scenario name to search for</param>
    /// <returns>Scenario matching the name, or null if not found</returns>
    ScenarioDefinition? GetByName(string name);

    /// <summary>
    /// Gets all scenarios that contain any of the specified tags.
    /// </summary>
    /// <param name="tags">Tag(s) to filter by</param>
    /// <returns>Enumerable of scenarios matching any tag</returns>
    IEnumerable<ScenarioDefinition> GetByTags(params string[] tags);

    /// <summary>
    /// Gets all scenarios that contain all of the specified tags.
    /// </summary>
    /// <param name="tags">Tag(s) that scenarios must contain</param>
    /// <returns>Enumerable of scenarios matching all tags</returns>
    IEnumerable<ScenarioDefinition> GetByAllTags(params string[] tags);

    /// <summary>
    /// Searches scenarios by description text (case-insensitive partial match).
    /// </summary>
    /// <param name="searchText">Text to search for in descriptions</param>
    /// <returns>Enumerable of scenarios with matching descriptions</returns>
    IEnumerable<ScenarioDefinition> SearchByDescription(string searchText);

    /// <summary>
    /// Gets the count of scenarios in the repository.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// In-memory implementation of scenario repository.
/// Suitable for development, testing, and small-scale deployments.
/// </summary>
public class InMemoryScenarioRepository : IScenarioRepository
{
    private readonly List<ScenarioDefinition> _scenarios;

    /// <inheritdoc/>
    public IEnumerable<ScenarioDefinition> GetAll()
    {
        return _scenarios.AsReadOnly();
    }

    /// <inheritdoc/>
    public ScenarioDefinition? GetByName(string name)
    {
        return _scenarios.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IEnumerable<ScenarioDefinition> GetByTags(params string[] tags)
    {
        if (tags == null || tags.Length == 0)
            return Enumerable.Empty<ScenarioDefinition>();

        return _scenarios.Where(s =>
            s.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <inheritdoc/>
    public IEnumerable<ScenarioDefinition> GetByAllTags(params string[] tags)
    {
        if (tags == null || tags.Length == 0)
            return _scenarios;

        return _scenarios.Where(s =>
            tags.All(tag => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <inheritdoc/>
    public IEnumerable<ScenarioDefinition> SearchByDescription(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return _scenarios;

        var lowerSearch = searchText.ToLowerInvariant();
        return _scenarios.Where(s =>
            s.Description.Contains(lowerSearch, StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains(lowerSearch, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public int Count => _scenarios.Count;

    /// <summary>
    /// Adds a scenario to the repository.
    /// </summary>
    /// <param name="scenario">Scenario to add</param>
    public void Add(ScenarioDefinition scenario)
    {
        if (scenario != null && !_scenarios.Contains(scenario))
        {
            _scenarios.Add(scenario);
        }
    }
}
