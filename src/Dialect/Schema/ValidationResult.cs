namespace Dialect.Core.Schema;

/// <summary>
/// Result of schema validation containing errors and warnings.
/// </summary>
public record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings
)
{
    /// <summary>
    /// Creates a validation result with only warnings.
    /// </summary>
    public static ValidationResult WithWarnings(params ValidationWarning[] warnings) =>
        new(IsValid: true, Errors: [], Warnings: warnings);

    /// <summary>
    /// Creates a validation result with errors (invalid).
    /// </summary>
    public static ValidationResult WithErrors(params ValidationError[] errors) =>
        new(IsValid: false, Errors: errors, Warnings: []);

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() =>
        new(IsValid: true, Errors: [], Warnings: []);

    /// <summary>
    /// Merges multiple validation results.
    /// </summary>
    public static ValidationResult Merge(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToList();
        var warnings = results.SelectMany(r => r.Warnings).ToList();
        return new(errors.Count == 0, errors, warnings);
    }
}

/// <summary>
/// Represents a validation error that prevents schema application.
/// </summary>
public record ValidationError(
    string RuleId,
    string Message,
    string AffectedEntity,
    int? LineNumber = null
);

/// <summary>
/// Represents a validation warning that does not prevent schema application.
/// </summary>
public record ValidationWarning(
    string RuleId,
    string Message,
    string AffectedEntity,
    int? LineNumber = null
);
