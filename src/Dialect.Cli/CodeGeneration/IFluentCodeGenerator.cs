namespace Dialect.Cli.CodeGeneration;

using Dialect.Core.QueryTranslation;

/// <summary>
/// Service for generating C# FluentBuilder code from SQL strings.
/// Orchestrates: SQL translation → AST parsing → FluentBuilder code emission.
/// </summary>
public interface IFluentCodeGenerator
{
    /// <summary>
    /// Converts a SQL string to equivalent FluentBuilder C# code.
    /// </summary>
    /// <param name="sqlContent">The SQL string to convert.</param>
    /// <returns>Generated C# code using FluentBuilder API, or null if conversion failed.</returns>
    string? GenerateFluentCode(string sqlContent);

    /// <summary>
    /// Gets the reason why conversion failed (for error reporting).
    /// </summary>
    string? GetLastConversionError();
}

/// <summary>
/// Default implementation using ISqlTranslator to parse and compile SQL.
/// Then emits FluentBuilder C# code from the AST.
/// </summary>
public sealed class DefaultFluentCodeGenerator : IFluentCodeGenerator
{
    private readonly ISqlTranslator _sqlTranslator;
    private string? _lastError;

    public DefaultFluentCodeGenerator(ISqlTranslator sqlTranslator)
    {
        _sqlTranslator = sqlTranslator ?? throw new ArgumentNullException(nameof(sqlTranslator));
    }

    /// <summary>
    /// Generates FluentBuilder code from SQL string.
    /// </summary>
    public string? GenerateFluentCode(string sqlContent)
    {
        _lastError = null;

        if (string.IsNullOrWhiteSpace(sqlContent))
        {
            _lastError = "SQL content cannot be empty";
            return null;
        }

        // For now: placeholder returning simple comment
        // In future: parse to AST and emit FluentBuilder method chains
        try
        {
            // TODO: Implement full FluentBuilder code generation
            // This requires:
            // 1. Parsing SQL to SelectStatement AST
            // 2. Emitting c# code like:
            //    new SelectBuilder()
            //        .Select(new Column { Name = "id" })
            //        .From(new TableReference { Name = "users" })
            //        .Where(...)
            //        .Build()

            return GeneratePlaceholderCode(sqlContent);
        }
        catch (Exception ex)
        {
            _lastError = $"Code generation failed: {ex.Message}";
            return null;
        }
    }

    public string? GetLastConversionError() => _lastError;

    /// <summary>
    /// Placeholder: generates a comment with the SQL.
    /// Will be replaced with actual FluentBuilder code generation.
    /// </summary>
    private static string GeneratePlaceholderCode(string sqlContent)
    {
        var truncated = sqlContent.Length > 60
            ? sqlContent[..60] + "..."
            : sqlContent;

        return $@"// SQL to FluentBuilder conversion placeholder
        // Original SQL: {truncated}
        new SelectBuilder()
            .Compile(dialect) // TODO: Generate full FluentBuilder chain";
    }
}
