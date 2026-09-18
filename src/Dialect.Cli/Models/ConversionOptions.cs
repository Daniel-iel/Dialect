namespace Dialect.Cli.Models;

using Dialect.Core.AST;
using Dialect.Core.Dialects;

/// <summary>
/// Configuration options for SQL to FluentBuilder conversion process.
/// </summary>
public sealed class ConversionOptions
{
    /// <summary>
    /// Scope of files/directories to convert.
    /// </summary>
    public required ConversionScope Scope { get; init; }

    /// <summary>
    /// Source SQL provider (auto-detected if null).
    /// </summary>
    public SqlProvider? SourceProvider { get; init; }

    /// <summary>
    /// Connection string for auto-detecting source provider.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Target SQL dialect for conversion.
    /// </summary>
    public required ISqlDialect TargetDialect { get; init; }

    /// <summary>
    /// Whether to perform a dry-run (no file modifications).
    /// </summary>
    public bool DryRun { get; init; } = true;

    /// <summary>
    /// Whether to create backup files before modification (.bak extension).
    /// </summary>
    public bool CreateBackups { get; init; } = true;

    /// <summary>
    /// Whether to include verbose logging during conversion.
    /// </summary>
    public bool Verbose { get; init; } = false;

    /// <summary>
    /// Whether to skip SQL strings with untranslatable constructs.
    /// False means error on untranslatable constructs.
    /// </summary>
    public bool AllowPartialTranslations { get; init; } = false;

    /// <summary>
    /// Output indentation (spaces or tabs).
    /// </summary>
    public string Indentation { get; init; } = "    ";

    /// <summary>
    /// Whether to format generated FluentBuilder code with Roslyn.
    /// </summary>
    public bool FormatCode { get; init; } = true;

    public override string ToString() =>
        $"ConversionOptions: {Scope}, DryRun={DryRun}, SourceProvider={SourceProvider}, Verbose={Verbose}";
}
