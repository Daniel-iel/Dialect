namespace Dialect.Samples.Utilities;

using System.Collections.Generic;

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
    /// Gets the count of scenarios in the repository.
    /// </summary>
    int Count { get; }
}
