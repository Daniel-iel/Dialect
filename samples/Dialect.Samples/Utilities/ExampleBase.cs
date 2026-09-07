namespace Dialect.Samples.Utilities;

using Dialect.Core.Dialects;
using Dialect.Core.AST;

/// <summary>
/// Represents a single example/scenario within an example class.
/// </summary>
public record ExampleScenario(
    string Name,
    Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters)> CompiledResults
);

/// <summary>
/// Base class for all example classes. Provides common functionality for running examples.
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
        Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
        Console.WriteLine($"  {Name}");
        Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");

        foreach (var (dialectName, (sql, parameters)) in results)
        {
            OutputFormatter.PrintDialectResult(dialectName, sql, parameters);
        }
    }

    /// <summary>
    /// Print a single query result.
    /// </summary>
    protected void PrintResult(string dialectName, string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        OutputFormatter.PrintDialectResult(dialectName, sql, parameters);
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
    /// Compile a single statement for a specific dialect.
    /// </summary>
    protected (string Sql, IReadOnlyDictionary<string, object?> Parameters) CompileForDialect(
        ISqlDialect dialect,
        Func<ISqlDialect, CompiledQuery> statementBuilder)
    {
        try
        {
            var compiled = statementBuilder(dialect);
            return (compiled.Sql, compiled.Parameters);
        }
        catch (Exception ex)
        {
            return ($"ERROR: {ex.Message}", new Dictionary<string, object?>());
        }
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

    /// <summary>
    /// Execute a SELECT query for all dialects and retrieve actual data.
    /// Returns SQL, parameters, results, execution time, and row count.
    /// </summary>
    protected async Task<Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters, List<dynamic> Results, long ExecutionTimeMs, int RowCount)>> ExecuteForAllDialectsAsync(
        Func<ISqlDialect, CompiledQuery> statementBuilder)
    {
        var results = new Dictionary<string, (string, IReadOnlyDictionary<string, object?>, List<dynamic>, long, int)>();

        foreach (var (dialectName, dialect) in DialectHelper.GetAllDialects())
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var compiled = statementBuilder(dialect);
                
                // Execute the query against the database
                using var connection = DapperExecutor.GetConnection(dialectName);
                await connection.OpenAsync();
                
                var queryResults = await DapperExecutor.QueryDynamicAsync(connection, compiled.Sql, compiled.Parameters);
                
                stopwatch.Stop();

                results[dialectName] = (
                    compiled.Sql,
                    compiled.Parameters,
                    queryResults,
                    stopwatch.ElapsedMilliseconds,
                    queryResults.Count
                );

                await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                results[dialectName] = (
                    $"ERROR: {ex.Message}",
                    new Dictionary<string, object?>(),
                    new List<dynamic>(),
                    0,
                    0
                );
            }
        }

        return results;
    }

    /// <summary>
    /// Execute a command (INSERT, UPDATE, DELETE) for all dialects.
    /// Returns SQL, parameters, rows affected, and execution time.
    /// </summary>
    protected async Task<Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters, List<dynamic> Results, long ExecutionTimeMs, int RowCount)>> ExecuteCommandForAllDialectsAsync(
        Func<ISqlDialect, CompiledQuery> statementBuilder)
    {
        var results = new Dictionary<string, (string, IReadOnlyDictionary<string, object?>, List<dynamic>, long, int)>();

        foreach (var (dialectName, dialect) in DialectHelper.GetAllDialects())
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var compiled = statementBuilder(dialect);
                
                // Execute the command against the database
                using var connection = DapperExecutor.GetConnection(dialectName);
                await connection.OpenAsync();
                
                int rowsAffected = await DapperExecutor.ExecuteAsync(connection, compiled.Sql, compiled.Parameters);
                
                stopwatch.Stop();

                // For commands, return empty results list with row count
                results[dialectName] = (
                    compiled.Sql,
                    compiled.Parameters,
                    new List<dynamic>(),
                    stopwatch.ElapsedMilliseconds,
                    rowsAffected
                );

                await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                results[dialectName] = (
                    $"ERROR: {ex.Message}",
                    new Dictionary<string, object?>(),
                    new List<dynamic>(),
                    0,
                    0
                );
            }
        }

        return results;
    }
}
