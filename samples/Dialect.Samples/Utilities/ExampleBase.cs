namespace Dialect.Samples.Utilities;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Represents a single example/scenario within an example class.
/// </summary>
public record ExampleScenario(
    string Name,
    Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> CompiledResults
);

/// <summary>
/// Base class for all example classes. Provides common functionality for running examples.
///
/// Supports traditional (imperative) pattern:
/// Override Run(), call AddScenario() for each scenario.
/// Maintains backward compatibility with existing example implementations.
/// </summary>
public abstract class ExampleBase
{
    protected readonly DialectHelper DialectHelper;
    protected readonly List<ExampleScenario> _collectedScenarios = new();

    public string Name { get; }
    public string Description { get; }

    public ExampleBase(string name, string description)
    {
        Name = name;
        Description = description;
        DialectHelper = new DialectHelper();
    }

    /// <summary>
    /// Run the example and display results.
    /// Override this method in subclasses to define example behavior.
    /// </summary>
    public abstract void Run();

    /// <summary>
    /// Add a scenario to the collected scenarios list for batch execution.
    /// Call this from sub-methods within Run() instead of PrintResults().
    /// Registers a query example for inclusion in the batch markdown report.
    /// In interactive mode, PrintResults() is called to display to console.
    /// In batch mode, scenarios are executed against the database and results captured in markdown.
    /// </summary>
    protected void AddScenario(string scenarioName, Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> compiledResults)
    {
        _collectedScenarios.Add(new ExampleScenario(scenarioName, compiledResults));
        PrintResults(compiledResults);
    }

    /// <summary>
    /// Get all scenarios collected during Run() execution.
    /// </summary>
    public List<ExampleScenario> GetCollectedScenarios()
    {
        return _collectedScenarios;
    }

    /// <summary>
    /// Print query results for all three dialects.
    /// </summary>
    protected void PrintResults(Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> results)
    {
        Console.Write(ResultsFormatter.FormatForConsole(Name, results));
    }

    /// <summary>
    /// Compile a statement to SQL for all three dialects.
    /// </summary>
    protected Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> CompileForAllDialects(
        Func<ISqlDialect, CompiledQuery> statementBuilder)
    {
        var results = new Dictionary<string, (string, IReadOnlyDictionary<string, object?>)>();

        foreach (var (dialectName, dialect) in DialectHelper.GetAllDialects())
        {
            try
            {
                var compiled = statementBuilder(dialect);
                results[dialectName] = (compiled.Sql, compiled.Parameters);
            }
            catch (Exception ex)
            {
                results[dialectName] = ($"ERROR: {ex.Message}", new Dictionary<string, object?>());
            }
        }

        return results;
    }

    /// <summary>
    /// Silent mode: Returns compiled results without printing to console.
    /// Used for batch execution and markdown file generation.
    /// </summary>
    public virtual Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> GetCompiledResults()
    {
        // Default implementation returns empty - subclasses should override
        return new Dictionary<string, (string, IReadOnlyDictionary<string, object?>)>();
    }
}
