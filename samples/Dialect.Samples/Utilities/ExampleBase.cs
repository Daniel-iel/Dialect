namespace Dialect.Samples.Utilities;

using Dialect.Core.Dialects;
using Dialect.Core.AST;

/// <summary>
/// Base class for all example classes. Provides common functionality for running examples.
/// </summary>
public abstract class ExampleBase
{
    protected readonly DialectHelper DialectHelper;

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
}
