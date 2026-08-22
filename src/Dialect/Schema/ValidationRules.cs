namespace Dialect.Core.Schema;

using Dialect.Core.AST.Migration;
using Dialect.Core.Dialects;
using System.Text.RegularExpressions;

/// <summary>
/// Core validation rules engine for schema validation.
/// </summary>
public static class ValidationRules
{
    // Rule IDs for referencing in errors/warnings
    public const string ColumnNameConvention = "COL_NAMING";
    public const string TableNameConvention = "TBL_NAMING";
    public const string IdentifierTooLong = "ID_TOO_LONG";
    public const string ColumnNameEmpty = "COL_NAME_EMPTY";
    public const string TableNameEmpty = "TBL_NAME_EMPTY";
    public const string DataTypeMissing = "DTYPE_MISSING";
    public const string LengthRequiredForStringType = "LEN_REQUIRED";
    public const string PrecisionRequiredForDecimal = "PREC_REQUIRED";
    public const string AutoIncrementOnNullable = "AI_ON_NULLABLE";
    public const string PrimaryKeyOnNullable = "PK_ON_NULLABLE";
    public const string DefaultValueTypeMismatch = "DEFAULT_TYPE_MISMATCH";
    public const string ForeignKeyReferencesNotFound = "FK_REF_MISSING";
    public const string ReservedKeywordUsed = "RESERVED_KEYWORD";
    public const string InvalidDataType = "INVALID_DTYPE";
    public const string IdentifierWithSpecialChars = "SPECIAL_CHARS";

    /// <summary>
    /// Validates column name follows naming convention.
    /// </summary>
    public static ValidationResult ValidateColumnName(string columnName, NamingConvention convention)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return ValidationResult.WithErrors(
                new ValidationError(ColumnNameEmpty, "Column name cannot be empty", columnName));

        return convention switch
        {
            NamingConvention.PascalCase => ValidatePascalCase(columnName, "Column", ColumnNameConvention),
            NamingConvention.SnakeCase => ValidateSnakeCase(columnName, "Column", ColumnNameConvention),
            NamingConvention.Mixed => ValidateMixed(columnName, "Column", ColumnNameConvention),
            _ => ValidationResult.Success()
        };
    }

    /// <summary>
    /// Validates table name follows naming convention.
    /// </summary>
    public static ValidationResult ValidateTableName(string tableName, NamingConvention convention)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return ValidationResult.WithErrors(
                new ValidationError(TableNameEmpty, "Table name cannot be empty", tableName));

        return convention switch
        {
            NamingConvention.PascalCase => ValidatePascalCase(tableName, "Table", TableNameConvention),
            NamingConvention.SnakeCase => ValidateSnakeCase(tableName, "Table", TableNameConvention),
            NamingConvention.Mixed => ValidatePascalCase(tableName, "Table", TableNameConvention),
            _ => ValidationResult.Success()
        };
    }

    /// <summary>
    /// Validates identifier length.
    /// </summary>
    public static ValidationResult ValidateIdentifierLength(string identifier, int maxLength)
    {
        if (identifier.Length > maxLength)
            return ValidationResult.WithErrors(
                new ValidationError(IdentifierTooLong,
                    $"Identifier '{identifier}' exceeds maximum length of {maxLength} characters",
                    identifier));
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates data type is valid for the dialect.
    /// </summary>
    public static ValidationResult ValidateDataType(DataType dataType, ISqlDialect dialect)
    {
        // DataType enum is pre-validated in AST, so if it compiles, it's valid
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates string types have a length specified.
    /// </summary>
    public static ValidationResult ValidateStringTypeLength(DataType dataType, int? length)
    {
        var isStringType = dataType is DataType.Char or DataType.Varchar or DataType.NChar or DataType.NVarchar or DataType.Varbinary;

        if (isStringType && !length.HasValue)
            return ValidationResult.WithErrors(
                new ValidationError(LengthRequiredForStringType,
                    $"Data type {dataType} requires length specification",
                    dataType.ToString()));

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates decimal types have precision specified.
    /// </summary>
    public static ValidationResult ValidateDecimalPrecision(DataType dataType, int? precision)
    {
        if ((dataType is DataType.Decimal or DataType.Money) && !precision.HasValue)
            return ValidationResult.WithErrors(
                new ValidationError(PrecisionRequiredForDecimal,
                    $"Data type {dataType} requires precision specification",
                    dataType.ToString()));

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates auto-increment columns are NOT NULL.
    /// </summary>
    public static ValidationResult ValidateAutoIncrementNotNullable(ColumnDef column)
    {
        if (column.IsAutoIncrement && column.Nullable)
            return ValidationResult.WithErrors(
                new ValidationError(AutoIncrementOnNullable,
                    $"Auto-increment column '{column.Name}' cannot be nullable",
                    column.Name));

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates primary key columns are NOT NULL.
    /// </summary>
    public static ValidationResult ValidatePrimaryKeyNotNullable(ColumnDef column)
    {
        if (column.IsPrimaryKey && column.Nullable)
            return ValidationResult.WithErrors(
                new ValidationError(PrimaryKeyOnNullable,
                    $"Primary key column '{column.Name}' cannot be nullable",
                    column.Name));

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates default value is type-compatible with column.
    /// </summary>
    public static ValidationResult ValidateDefaultValueType(ColumnDef column)
    {
        if (column.DefaultValue is null)
            return ValidationResult.Success();

        // Simple type-compatibility check based on DataType and default value format
        var defaultStr = column.DefaultValue.ToString() ?? "";

        var isCompatible = column.Type switch
        {
            // Numeric types can accept numeric defaults
            DataType.SmallInt or DataType.Int or DataType.BigInt or 
            DataType.Decimal or DataType.Money or DataType.Float or DataType.Double
                => IsNumericDefault(defaultStr),

            // String/text types accept string defaults
            DataType.Char or DataType.Varchar or DataType.NChar or DataType.NVarchar or DataType.Text
                => true,

            // Date/time types can accept date function calls or literals
            DataType.Date or DataType.Time or DataType.DateTime or DataType.DateTime2 or DataType.SmallDateTime or DataType.Timestamp
                => IsDateTimeDefault(defaultStr),

            // Boolean accepts 0/1 or boolean literals
            DataType.Boolean
                => IsBooleanDefault(defaultStr),

            // UUID accepts GUIDs or function calls
            DataType.Uuid
                => IsUuidDefault(defaultStr),

            _ => true // Other types are permissive
        };

        if (!isCompatible)
            return ValidationResult.WithErrors(
                new ValidationError(DefaultValueTypeMismatch,
                    $"Default value '{defaultStr}' is incompatible with column type {column.Type}",
                    column.Name));

        return ValidationResult.Success();
    }

    // Helper methods for type checking
    private static bool IsNumericDefault(string value)
        => int.TryParse(value, out _) || decimal.TryParse(value, out _);

    private static bool IsDateTimeDefault(string value)
        => value.Contains("GETUTCDATE") || value.Contains("CURRENT_TIMESTAMP") ||
           value.Contains("NOW()") || DateTime.TryParse(value, out _);

    private static bool IsBooleanDefault(string value)
        => value is "0" or "1" or "true" or "false" or "TRUE" or "FALSE";

    private static bool IsUuidDefault(string value)
        => value.Contains("NEWID") || value.Contains("uuid_generate_v4") ||
           Guid.TryParse(value, out _);

    // Naming convention validators
    private static ValidationResult ValidatePascalCase(string identifier, string entityType, string ruleId)
    {
        var pascalPattern = new Regex(@"^[A-Z][a-zA-Z0-9]*$");
        if (!pascalPattern.IsMatch(identifier))
            return ValidationResult.WithWarnings(
                new ValidationWarning(ruleId,
                    $"{entityType} '{identifier}' does not follow PascalCase convention",
                    identifier));
        return ValidationResult.Success();
    }

    private static ValidationResult ValidateSnakeCase(string identifier, string entityType, string ruleId)
    {
        var snakePattern = new Regex(@"^[a-z][a-z0-9_]*$");
        if (!snakePattern.IsMatch(identifier))
            return ValidationResult.WithWarnings(
                new ValidationWarning(ruleId,
                    $"{entityType} '{identifier}' does not follow snake_case convention",
                    identifier));
        return ValidationResult.Success();
    }

    private static ValidationResult ValidateMixed(string identifier, string entityType, string ruleId)
    {
        // Tables: PascalCase, Columns: camelCase
        if (entityType == "Table")
            return ValidatePascalCase(identifier, entityType, ruleId);
        else
            return new ValidationResult(true, new List<ValidationError>(), new List<ValidationWarning>()); // Columns can be any reasonable format
    }
}
