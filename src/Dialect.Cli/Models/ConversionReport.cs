namespace Dialect.Cli.Models;

/// <summary>
/// Report generated after attempting to convert C# source files.
/// Includes statistics on files processed, SQL strings found, conversions performed/skipped.
/// </summary>
public sealed class ConversionReport
{
    /// <summary>
    /// Total C# source files scanned.
    /// </summary>
    public int TotalFilesScanned { get; init; }

    /// <summary>
    /// Number of files containing SQL strings.
    /// </summary>
    public int FilesWithSqlFound { get; init; }

    /// <summary>
    /// Total SQL string literals discovered across all files.
    /// </summary>
    public int TotalSqlStringsFound { get; init; }

    /// <summary>
    /// SQL strings successfully converted to FluentBuilder equivalents.
    /// </summary>
    public int SuccessfulConversions { get; init; }

    /// <summary>
    /// SQL strings skipped (e.g., untranslatable constructs detected).
    /// </summary>
    public int SkippedConversions { get; init; }

    /// <summary>
    /// Conversion errors encountered (parse failures, etc.).
    /// </summary>
    public int ConversionErrors { get; init; }

    /// <summary>
    /// Detailed results for each converted file.
    /// </summary>
    public IReadOnlyList<FileConversionResult> FileResults { get; init; } = [];

    /// <summary>
    /// Whether any files were actually modified.
    /// </summary>
    public bool HasModifications => SuccessfulConversions > 0;

    /// <summary>
    /// Overall success rate (successful / total found).
    /// </summary>
    public double SuccessRate =>
        TotalSqlStringsFound == 0 ? 0.0 : (double)SuccessfulConversions / TotalSqlStringsFound;

    public override string ToString() =>
        $"Conversion Report: {TotalFilesScanned} files scanned, " +
        $"{TotalSqlStringsFound} SQL strings found, " +
        $"{SuccessfulConversions} converted ({SuccessRate:P}), " +
        $"{SkippedConversions} skipped, " +
        $"{ConversionErrors} errors";
}

/// <summary>
/// Result of converting SQL strings in a single file.
/// </summary>
public sealed class FileConversionResult
{
    /// <summary>
    /// Path to the C# source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Number of SQL strings found in this file.
    /// </summary>
    public int SqlStringsFound { get; init; }

    /// <summary>
    /// Number of successful conversions in this file.
    /// </summary>
    public int SuccessfulConversions { get; init; }

    /// <summary>
    /// Number of skipped conversions in this file.
    /// </summary>
    public int SkippedConversions { get; init; }

    /// <summary>
    /// Conversion results per SQL string (line number, original SQL, converted code).
    /// </summary>
    public IReadOnlyList<SqlConversionResult> SqlResults { get; init; } = [];

    public override string ToString() =>
        $"{FilePath}: {SqlStringsFound} SQL, {SuccessfulConversions} converted, {SkippedConversions} skipped";
}

/// <summary>
/// Result of converting a single SQL string to FluentBuilder code.
/// </summary>
public sealed class SqlConversionResult
{
    /// <summary>
    /// Line number where SQL string was found (1-based).
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The original SQL string.
    /// </summary>
    public required string OriginalSql { get; init; }

    /// <summary>
    /// The FluentBuilder C# code (if conversion succeeded).
    /// </summary>
    public string? ConvertedCode { get; init; }

    /// <summary>
    /// Whether conversion was successful.
    /// </summary>
    public bool IsSuccessful => ConvertedCode is not null;

    /// <summary>
    /// Reason for skipping or error (if not successful).
    /// </summary>
    public string? FailureReason { get; init; }

    public override string ToString() =>
        IsSuccessful
            ? $"Line {LineNumber}: Converted (→ {ConvertedCode?.Length} chars)"
            : $"Line {LineNumber}: {FailureReason}";
}
