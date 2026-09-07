namespace Dialect.Samples.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dialect.Samples.Utilities;

/// <summary>
/// Centralized orchestrator for executing example scenarios across all dialects.
/// Provides unified execution flow management and result aggregation.
/// </summary>
public class ExampleExecutionOrchestrator
{
    private readonly IScenarioRepository _scenarioRepository;
    private readonly List<string> _executionLog;
    private DateTime _executionStartTime;

    /// <summary>
    /// Initializes orchestrator with a scenario repository.
    /// </summary>
    /// <param name="scenarioRepository">Repository providing scenario definitions</param>
    public ExampleExecutionOrchestrator(IScenarioRepository scenarioRepository)
    {
        _scenarioRepository = scenarioRepository ?? throw new ArgumentNullException(nameof(scenarioRepository));
        _executionLog = new List<string>();
    }

    /// <summary>
    /// Gets the execution log from last orchestration run.
    /// </summary>
    public IReadOnlyList<string> ExecutionLog => _executionLog.AsReadOnly();

    /// <summary>
    /// Gets the total count of scenarios in the repository.
    /// </summary>
    public int ScenarioCount => _scenarioRepository.Count;

    /// <summary>
    /// Executes all scenarios from the repository with provided executor.
    /// </summary>
    /// <param name="executor">Function to execute a scenario and return results</param>
    /// <param name="shouldLogExecution">Whether to log execution details</param>
    /// <returns>Aggregated execution results</returns>
    public ExecutionResults ExecuteAllScenarios(
        Func<ScenarioDefinition, ScenarioExecutionResult> executor,
        bool shouldLogExecution = true)
    {
        if (executor == null)
            throw new ArgumentNullException(nameof(executor));

        _executionLog.Clear();
        _executionStartTime = DateTime.UtcNow;

        if (shouldLogExecution)
            LogExecution($"Starting orchestration: {_scenarioRepository.Count} scenarios");

        var results = new List<ScenarioExecutionResult>();

        foreach (var scenario in _scenarioRepository.GetAll())
        {
            try
            {
                if (shouldLogExecution)
                    LogExecution($"Executing scenario: {scenario.Name}");

                var result = executor(scenario);
                results.Add(result);

                if (shouldLogExecution)
                    LogExecution($"✓ Completed: {scenario.Name}");
            }
            catch (Exception ex)
            {
                if (shouldLogExecution)
                    LogExecution($"✗ Failed: {scenario.Name} - {ex.Message}");
            }
        }

        if (shouldLogExecution)
            LogExecution($"Orchestration complete: {results.Count}/{_scenarioRepository.Count} succeeded");

        return new ExecutionResults(results, DateTime.UtcNow - _executionStartTime);
    }

    /// <summary>
    /// Executes scenarios matching the specified tag.
    /// </summary>
    /// <param name="tag">Tag to filter scenarios</param>
    /// <param name="executor">Function to execute a scenario</param>
    /// <param name="shouldLogExecution">Whether to log execution details</param>
    /// <returns>Execution results for matching scenarios</returns>
    public ExecutionResults ExecuteScenariosByTag(
        string tag,
        Func<ScenarioDefinition, ScenarioExecutionResult> executor,
        bool shouldLogExecution = true)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or whitespace", nameof(tag));

        var matchingScenarios = _scenarioRepository.GetByTags(tag);
        return ExecuteScenarios(matchingScenarios, executor, $"tag: {tag}", shouldLogExecution);
    }

    /// <summary>
    /// Executes a scenario by exact name match.
    /// </summary>
    /// <param name="scenarioName">Exact name of scenario to execute</param>
    /// <param name="executor">Function to execute the scenario</param>
    /// <param name="shouldLogExecution">Whether to log execution details</param>
    /// <returns>Execution result, or null if scenario not found</returns>
    public ScenarioExecutionResult? ExecuteScenarioByName(
        string scenarioName,
        Func<ScenarioDefinition, ScenarioExecutionResult> executor,
        bool shouldLogExecution = true)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
            throw new ArgumentException("Scenario name cannot be null or whitespace", nameof(scenarioName));

        var scenario = _scenarioRepository.GetByName(scenarioName);
        if (scenario == null)
        {
            if (shouldLogExecution)
                LogExecution($"Scenario not found: {scenarioName}");
            return null;
        }

        if (shouldLogExecution)
            LogExecution($"Executing scenario: {scenarioName}");

        try
        {
            var result = executor(scenario);
            if (shouldLogExecution)
                LogExecution($"✓ Completed: {scenarioName}");
            return result;
        }
        catch (Exception ex)
        {
            if (shouldLogExecution)
                LogExecution($"✗ Failed: {scenarioName} - {ex.Message}");
            throw;
        }
    }


    /// <summary>
    /// Gets a summary report of the orchestration state.
    /// </summary>
    /// <returns>Human-readable orchestration summary</returns>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Orchestration Summary ===");
        sb.AppendLine($"Total Scenarios: {_scenarioRepository.Count}");
        sb.AppendLine($"Execution Log Entries: {_executionLog.Count}");
        sb.AppendLine();

        if (_executionLog.Count > 0)
        {
            sb.AppendLine("Recent Log:");
            var recentLogs = _executionLog.TakeLast(5);
            foreach (var logEntry in recentLogs)
            {
                sb.AppendLine($"  {logEntry}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets all scenarios from the repository.
    /// </summary>
    public IEnumerable<ScenarioDefinition> GetAllScenarios()
    {
        return _scenarioRepository.GetAll();
    }

    /// <summary>
    /// Gets scenarios filtered by tag.
    /// </summary>
    public IEnumerable<ScenarioDefinition> GetScenariosByTag(string tag)
    {
        return _scenarioRepository.GetByTags(tag);
    }

    // Private helper methods

    private ExecutionResults ExecuteScenarios(
        IEnumerable<ScenarioDefinition> scenarios,
        Func<ScenarioDefinition, ScenarioExecutionResult> executor,
        string filterDescription,
        bool shouldLogExecution)
    {
        _executionLog.Clear();
        _executionStartTime = DateTime.UtcNow;

        var scenarioList = scenarios.ToList();
        if (shouldLogExecution)
            LogExecution($"Starting orchestration ({filterDescription}): {scenarioList.Count} scenarios");

        var results = new List<ScenarioExecutionResult>();

        foreach (var scenario in scenarioList)
        {
            try
            {
                if (shouldLogExecution)
                    LogExecution($"Executing scenario: {scenario.Name}");

                var result = executor(scenario);
                results.Add(result);

                if (shouldLogExecution)
                    LogExecution($"✓ Completed: {scenario.Name}");
            }
            catch (Exception ex)
            {
                if (shouldLogExecution)
                    LogExecution($"✗ Failed: {scenario.Name} - {ex.Message}");
            }
        }

        if (shouldLogExecution)
            LogExecution($"Orchestration complete: {results.Count}/{scenarioList.Count} succeeded");

        return new ExecutionResults(results, DateTime.UtcNow - _executionStartTime);
    }

    private void LogExecution(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _executionLog.Add($"[{timestamp}] {message}");
    }
}

/// <summary>
/// Aggregated results from orchestration execution.
/// </summary>
public record ExecutionResults(
    IReadOnlyList<ScenarioExecutionResult> Results,
    TimeSpan ExecutionDuration
)
{
    /// <summary>
    /// Gets the count of successful executions.
    /// </summary>
    public int SuccessCount => Results.Count(r => IsSuccessful(r));

    /// <summary>
    /// Gets the count of failed executions.
    /// </summary>
    public int FailureCount => Results.Count(r => !IsSuccessful(r));

    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public decimal SuccessRate =>
        Results.Count == 0 ? 0 : (SuccessCount * 100m) / Results.Count;

    /// <summary>
    /// Gets summary text for the results.
    /// </summary>
    public string GetSummary()
    {
        return $"Executed {Results.Count} scenarios in {ExecutionDuration.TotalSeconds:F2}s: " +
               $"{SuccessCount} succeeded, {FailureCount} failed ({SuccessRate:F1}% success rate)";
    }

    /// <summary>
    /// Determines if a scenario execution was successful (no errors reported).
    /// </summary>
    private static bool IsSuccessful(ScenarioExecutionResult result)
    {
        // A scenario is successful if it has no errors in any dialect's execution
        return !result.ExecutedResults.Values.Any(r => !string.IsNullOrEmpty(r.Error));
    }
}
