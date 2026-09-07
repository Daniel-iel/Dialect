using Dialect.Samples._01_Basic;
using Dialect.Samples._02_Intermediate;
using Dialect.Samples._03_Advanced;
using Dialect.Samples.Services;
using Dialect.Samples.Utilities;

namespace Dialect.Samples.Executors;

/// <summary>
/// Executes all examples in batch mode, collecting compiled queries without printing to console.
/// Generates a markdown file with all results organized in tables.
/// </summary>
public class BatchExampleExecutor
{
    private readonly ExecutionProgressReporter _progressReporter;
    private readonly MarkdownResultsWriter _resultsWriter;
    private readonly List<ExampleBase> _allExamples;
    private readonly DatabaseConfiguration _databaseConfig;
    private readonly Dictionary<string, QueryExecutor> _executors;

    public BatchExampleExecutor(DatabaseConfiguration? config = null)
    {
        _progressReporter = new ExecutionProgressReporter();
        _resultsWriter = new MarkdownResultsWriter();
        _allExamples = new List<ExampleBase>();
        _databaseConfig = config ?? DatabaseConfiguration.LoadFromEnvironment();
        _executors = _databaseConfig.GetAllExecutors();
    }

    /// <summary>
    /// Execute all examples and generate markdown report.
    /// </summary>
    public async Task<string> ExecuteAll()
    {
        InitializeExamples();
        _progressReporter.Initialize(_allExamples.Count);
        _resultsWriter.Initialize();

        foreach (var example in _allExamples)
        {
            await ExecuteExampleAsync(example);
        }

        var filePath = _resultsWriter.SaveToFile();
        _progressReporter.ReportFinalPath(filePath);

        return filePath;
    }

    /// <summary>
    /// Initialize all example instances.
    /// </summary>
    private void InitializeExamples()
    {
        // Basic Examples
        _allExamples.Add(new SelectExamples());
        _allExamples.Add(new InsertExamples());
        _allExamples.Add(new UpdateExamples());
        _allExamples.Add(new DeleteExamples());
        _allExamples.Add(new UpsertExamples());

        // Intermediate Examples
        _allExamples.Add(new JoinExamples());
        _allExamples.Add(new CteExamples());
        _allExamples.Add(new WindowFunctionExamples());
        _allExamples.Add(new GroupingExamples());

        // Advanced Examples
        _allExamples.Add(new OptimizationExamples());
        _allExamples.Add(new IndexAdvisorExamples());
        _allExamples.Add(new SchemaValidationExamples());
    }

    /// <summary>
    /// Execute a single example and collect results.
    /// </summary>
    private async Task ExecuteExampleAsync(ExampleBase example)
    {
        _progressReporter.ReportStarting(example.Name);

        try
        {
            // Call Run() to verify it executes without errors
            CaptureConsoleOutput(() =>
            {
                try
                {
                    example.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                    }
                }
            });

            // Get scenarios collected during Run() execution
            var scenarios = example.GetCollectedScenarios();

            if (scenarios.Count > 0)
            {
                // Execute each scenario and add to results
                foreach (var scenario in scenarios)
                {
                    var dialectResults = new Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters, List<dynamic> Results, long ExecutionTimeMs, int RowCount)>();

                    foreach (var (dialectName, (sql, parameters)) in scenario.CompiledResults)
                    {
                        var (results, timeMs, rowCount) = await ExecuteQueryAsync(dialectName, sql, parameters);
                        dialectResults[dialectName] = (sql, parameters, results, timeMs, rowCount);
                    }

                    // Add the scenario results - use scenario name as the example name in markdown
                    _resultsWriter.AddExampleResult(scenario.Name, dialectResults);
                }
            }
            else
            {
                // Fallback to old behavior: use GetCompiledResults()
                var compiledResults = example.GetCompiledResults();

                if (compiledResults.Count > 0)
                {
                    var dialectResults = new Dictionary<string, (string Sql, IReadOnlyDictionary<string, object?> Parameters, List<dynamic> Results, long ExecutionTimeMs, int RowCount)>();

                    foreach (var (dialectName, (sql, parameters)) in compiledResults)
                    {
                        var (results, timeMs, rowCount) = await ExecuteQueryAsync(dialectName, sql, parameters);
                        dialectResults[dialectName] = (sql, parameters, results, timeMs, rowCount);
                    }

                    // Add the structured results to the markdown writer
                    _resultsWriter.AddExampleResult(example.Name, dialectResults);
                }
            }

            _progressReporter.ReportCompleted(example.Name);
        }
        catch (Exception ex)
        {
            // Log error to both progress reporter (console) and results writer (markdown)
            _progressReporter.ReportError(example.Name, ex);
            _resultsWriter.AddErrorResult(example.Name, ex);
        }
    }

    /// <summary>
    /// Execute a compiled SQL query against a specific dialect's database.
    /// </summary>
    private async Task<(List<dynamic> Results, long TimeMs, int RowCount)> ExecuteQueryAsync(
        string dialectName,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        try
        {
            if (!_executors.TryGetValue(dialectName, out var executor))
            {
                // Dialect not configured
                return (new List<dynamic>(), 0, 0);
            }

            var paramDict = new Dictionary<string, object?>(parameters);

            // Determine if this is a SELECT or a non-query statement
            var trimmedSql = sql.Trim();
            bool isSelectQuery = trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                                trimmedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

            ExecutionResult result;
            if (isSelectQuery)
            {
                result = await executor.ExecuteQueryAsync(sql, paramDict);
            }
            else
            {
                result = await executor.ExecuteNonQueryAsync(sql, paramDict);
            }

            if (result.Error != null)
            {
                // Execution failed, return empty results
                return (new List<dynamic>(), 0, 0);
            }

            return (result.Rows, result.ExecutionTimeMs, result.RowCount);
        }
        catch (Exception ex)
        {
            // Log but don't throw - continue with other dialects
            _progressReporter.ReportExecutionWarning($"Failed to execute on {dialectName}: {ex.Message}");
            return (new List<dynamic>(), 0, 0);
        }
    }

    /// <summary>
    /// Captures console output from a given action.
    /// </summary>
    private string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            try
            {
                action.Invoke();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
