namespace Dialect.PostgreSql.Schema;

using Dialect.Core.AST.Migration;
using Dialect.Core.Schema;
using Dialect.Core.Dialects;

/// <summary>
/// PostgreSQL-specific schema validator (13+).
/// Enforces PostgreSQL naming conventions, identifier limits (63 chars),
/// data type compatibility, and constraint rules.
/// </summary>
public class PostgreSqlSchemaValidator : SchemaValidator
{
    // PostgreSQL reserved keywords (common subset)
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "join", "insert", "update", "delete", "create", "table",
        "drop", "alter", "add", "constraint", "primary", "key", "foreign", "references",
        "index", "on", "unique", "null", "default", "serial", "bigserial", "generated",
        "always", "identity", "values", "set", "grant", "revoke", "execute", "function",
        "procedure", "return", "begin", "end", "if", "else", "loop", "while", "case",
        "when", "then", "order", "by", "group", "having", "distinct", "all", "any",
        "exists", "in", "like", "between", "union", "intersect", "except", "cast"
    };

    public override NamingConvention NamingConvention => NamingConvention.SnakeCase;

    public override int MaxIdentifierLength => 63;

    public override ValidationResult Validate(Migration migration, ISqlDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(migration.Name))
            return ValidationResult.WithErrors(
                new ValidationError("MIGRATION_NAME_EMPTY", "Migration name cannot be empty", "Migration"));

        if (migration.Steps.Count == 0)
            return ValidationResult.WithErrors(
                new ValidationError("MIGRATION_NO_STEPS", "Migration must contain at least one step", migration.Name));

        var results = new List<ValidationResult>
        {
            ValidateMigrationName(migration.Name)
        };

        foreach (var step in migration.Steps)
        {
            results.Add(ValidateStep(step, dialect));
        }

        return ValidationResult.Merge(results.ToArray());
    }

    public override ValidationResult ValidateColumn(ColumnDef column, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateColumnName(column.Name, NamingConvention),
            ValidationRules.ValidateIdentifierLength(column.Name, MaxIdentifierLength),
            ValidationRules.ValidateDataType(column.Type, dialect),
            ValidationRules.ValidateStringTypeLength(column.Type, column.Length),
            ValidationRules.ValidateDecimalPrecision(column.Type, column.Precision?.Precision),
            ValidationRules.ValidateAutoIncrementNotNullable(column),
            ValidationRules.ValidatePrimaryKeyNotNullable(column),
            ValidationRules.ValidateDefaultValueType(column)
        };

        return ValidationResult.Merge(results.ToArray());
    }

    public override ValidationResult ValidateTable(string tableName, ColumnDef[] columns, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(tableName, NamingConvention),
            ValidationRules.ValidateIdentifierLength(tableName, MaxIdentifierLength)
        };

        foreach (var column in columns)
        {
            results.Add(ValidateColumn(column, dialect));
        }

        return ValidationResult.Merge(results.ToArray());
    }

    public override ValidationResult ValidateStep(MigrationStep step, ISqlDialect dialect)
    {
        return step switch
        {
            CreateTableStep cts => ValidateCreateTable(cts, dialect),
            DropTableStep dts => ValidateDropTable(dts, dialect),
            AddColumnStep acs => ValidateAddColumn(acs, dialect),
            DropColumnStep dcs => ValidateDropColumn(dcs, dialect),
            AlterColumnStep als => ValidateAlterColumn(als, dialect),
            AddIndexStep ais => ValidateAddIndex(ais, dialect),
            DropIndexStep dis => ValidateDropIndex(dis, dialect),
            AddForeignKeyStep afks => ValidateAddForeignKey(afks, dialect),
            DropForeignKeyStep dfks => ValidateDropForeignKey(dfks, dialect),
            _ => ValidationResult.Success()
        };
    }

    private static ValidationResult ValidateMigrationName(string name)
    {
        if (ReservedKeywords.Contains(name))
            return ValidationResult.WithWarnings(
                new ValidationWarning(ValidationRules.ReservedKeywordUsed,
                    $"Migration name '{name}' is a reserved PostgreSQL keyword",
                    name));
        return ValidationResult.Success();
    }

    private ValidationResult ValidateCreateTable(CreateTableStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateIdentifierLength(step.TableName, MaxIdentifierLength)
        };

        foreach (var column in step.Columns)
        {
            results.Add(ValidateColumn(column, dialect));
        }

        if (step.PrimaryKeyColumns.Count > 0)
        {
            var columnNames = step.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var pkColumn in step.PrimaryKeyColumns)
            {
                if (!columnNames.Contains(pkColumn))
                    results.Add(ValidationResult.WithErrors(
                        new ValidationError("PK_COLUMN_NOT_FOUND",
                            $"Primary key column '{pkColumn}' not found in table definition",
                            pkColumn)));
            }
        }

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateDropTable(DropTableStep step, ISqlDialect dialect)
    {
        return ValidationRules.ValidateTableName(step.TableName, NamingConvention);
    }

    private ValidationResult ValidateAddColumn(AddColumnStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidateColumn(step.Column, dialect)
        };

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateDropColumn(DropColumnStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateColumnName(step.ColumnName, NamingConvention)
        };

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateAlterColumn(AlterColumnStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateColumnName(step.ColumnName, NamingConvention)
        };

        if (step.NewType.HasValue)
        {
            results.Add(ValidationRules.ValidateStringTypeLength(step.NewType.Value, null));
        }

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateAddIndex(AddIndexStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention)
        };

        foreach (var column in step.ColumnNames)
        {
            results.Add(ValidationRules.ValidateColumnName(column, NamingConvention));
        }

        if (!string.IsNullOrEmpty(step.IndexName))
        {
            results.Add(ValidationRules.ValidateIdentifierLength(step.IndexName, MaxIdentifierLength));
        }

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateDropIndex(DropIndexStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateIdentifierLength(step.IndexName, MaxIdentifierLength)
        };

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateAddForeignKey(AddForeignKeyStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateColumnName(step.ColumnName, NamingConvention),
            ValidationRules.ValidateTableName(step.ReferencedTable, NamingConvention),
            ValidationRules.ValidateColumnName(step.ReferencedColumn, NamingConvention)
        };

        if (!string.IsNullOrEmpty(step.ConstraintName))
        {
            results.Add(ValidationRules.ValidateIdentifierLength(step.ConstraintName, MaxIdentifierLength));
        }

        return ValidationResult.Merge(results.ToArray());
    }

    private ValidationResult ValidateDropForeignKey(DropForeignKeyStep step, ISqlDialect dialect)
    {
        var results = new List<ValidationResult>
        {
            ValidationRules.ValidateTableName(step.TableName, NamingConvention),
            ValidationRules.ValidateIdentifierLength(step.ConstraintName, MaxIdentifierLength)
        };

        return ValidationResult.Merge(results.ToArray());
    }
}




