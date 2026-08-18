namespace Dialect.PostgreSql.Rendering;

using Dialect.Core.AST;
using Dialect.Core.AST.Migration;
using Dialect.Core.Dialects;

/// <summary>
/// Renders migration steps to PostgreSQL SQL.
/// </summary>
public sealed class PostgreSqlMigrationRenderer : IMigrationRenderer
{
    public CompiledQuery Render(Migration migration, ISqlDialect dialect)
    {
        if (migration == null)
            throw new ArgumentNullException(nameof(migration));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var sqlLines = new List<string>();

        foreach (var step in migration.Steps)
        {
            sqlLines.Add(RenderStep(step, dialect) + ";");
        }

        var sql = string.Join("\n", sqlLines);
        return new CompiledQuery(sql, new Dictionary<string, object?>());
    }

    public string RenderStep(MigrationStep step, ISqlDialect dialect)
    {
        return step switch
        {
            CreateTableStep cts => RenderCreateTable(cts, dialect),
            DropTableStep dts => RenderDropTable(dts, dialect),
            AddColumnStep acs => RenderAddColumn(acs, dialect),
            DropColumnStep dcs => RenderDropColumn(dcs, dialect),
            AlterColumnStep altcs => RenderAlterColumn(altcs, dialect),
            AddIndexStep ais => RenderAddIndex(ais, dialect),
            DropIndexStep dis => RenderDropIndex(dis, dialect),
            AddForeignKeyStep afks => RenderAddForeignKey(afks, dialect),
            DropForeignKeyStep dfks => RenderDropForeignKey(dfks, dialect),
            _ => throw new InvalidOperationException($"Unknown migration step type: {step.GetType().Name}")
        };
    }

    private string RenderCreateTable(CreateTableStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var columnDefs = new List<string>();

        foreach (var col in step.Columns)
        {
            columnDefs.Add(RenderColumnDefinition(col, dialect));
        }

        var sql = new System.Text.StringBuilder();
        sql.AppendLine($"CREATE TABLE {tableName} (");

        for (int i = 0; i < columnDefs.Count; i++)
        {
            sql.Append("    " + columnDefs[i]);
            if (i < columnDefs.Count - 1 || (step.PrimaryKeyColumns != null && step.PrimaryKeyColumns.Count > 0))
                sql.AppendLine(",");
            else
                sql.AppendLine();
        }

        if (step.PrimaryKeyColumns != null && step.PrimaryKeyColumns.Count > 0)
        {
            var pkColumns = string.Join(", ", step.PrimaryKeyColumns.Select(c => QuoteIdentifier(c)));
            sql.AppendLine($"    PRIMARY KEY ({pkColumns})");
        }

        sql.Append(")");

        return sql.ToString();
    }

    private string RenderDropTable(DropTableStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var cascade = step.Cascade ? " CASCADE" : "";
        return $"DROP TABLE IF EXISTS {tableName}{cascade}";
    }

    private string RenderAddColumn(AddColumnStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var columnDef = RenderColumnDefinition(step.Column, dialect);
        return $"ALTER TABLE {tableName} ADD COLUMN {columnDef}";
    }

    private string RenderDropColumn(DropColumnStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var columnName = QuoteIdentifier(step.ColumnName);
        return $"ALTER TABLE {tableName} DROP COLUMN {columnName}";
    }

    private string RenderAlterColumn(AlterColumnStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var columnName = QuoteIdentifier(step.ColumnName);

        var statements = new List<string>();

        if (step.NewType.HasValue)
        {
            var type = GetDataTypeSql(step.NewType.Value, dialect);
            statements.Add($"ALTER TABLE {tableName} ALTER COLUMN {columnName} TYPE {type}");
        }

        if (step.Nullable.HasValue)
        {
            var nullable = step.Nullable.Value ? "DROP NOT NULL" : "SET NOT NULL";
            statements.Add($"ALTER TABLE {tableName} ALTER COLUMN {columnName} {nullable}");
        }

        if (step.DefaultValue != null)
        {
            statements.Add($"ALTER TABLE {tableName} ALTER COLUMN {columnName} SET DEFAULT {FormatValue(step.DefaultValue)}");
        }

        return string.Join("\n", statements);
    }

    private string RenderAddIndex(AddIndexStep step, ISqlDialect dialect)
    {
        var indexName = QuoteIdentifier(step.IndexName ?? $"ix_{step.TableName}_{string.Join("_", step.ColumnNames)}");
        var tableName = QuoteIdentifier(step.TableName);
        var columns = string.Join(", ", step.ColumnNames.Select(c => QuoteIdentifier(c)));
        var unique = step.IsUnique ? "UNIQUE " : "";

        return $"CREATE {unique}INDEX {indexName} ON {tableName} ({columns})";
    }

    private string RenderDropIndex(DropIndexStep step, ISqlDialect dialect)
    {
        var indexName = QuoteIdentifier(step.IndexName);
        return $"DROP INDEX IF EXISTS {indexName}";
    }

    private string RenderAddForeignKey(AddForeignKeyStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var columnName = QuoteIdentifier(step.ColumnName);
        var refTable = QuoteIdentifier(step.ReferencedTable);
        var refColumn = QuoteIdentifier(step.ReferencedColumn);
        var constraintName = QuoteIdentifier(step.ConstraintName ?? $"fk_{step.TableName}_{step.ColumnName}");
        var cascade = step.OnDeleteCascade ? " ON DELETE CASCADE" : "";

        return $"ALTER TABLE {tableName} ADD CONSTRAINT {constraintName} FOREIGN KEY ({columnName}) REFERENCES {refTable}({refColumn}){cascade}";
    }

    private string RenderDropForeignKey(DropForeignKeyStep step, ISqlDialect dialect)
    {
        var tableName = QuoteIdentifier(step.TableName);
        var constraintName = QuoteIdentifier(step.ConstraintName);
        return $"ALTER TABLE {tableName} DROP CONSTRAINT {constraintName}";
    }

    private string RenderColumnDefinition(ColumnDef column, ISqlDialect dialect)
    {
        var columnName = QuoteIdentifier(column.Name);
        var type = GetDataTypeSql(column.Type, dialect);
        var nullable = column.Nullable ? "" : " NOT NULL";
        var identity = column.IsAutoIncrement ? " GENERATED ALWAYS AS IDENTITY" : "";
        var defaultClause = column.DefaultValue != null ? $" DEFAULT {FormatValue(column.DefaultValue)}" : "";

        return $"{columnName} {type}{identity}{nullable}{defaultClause}".Trim();
    }

    private string GetDataTypeSql(DataType type, ISqlDialect dialect)
    {
        return type switch
        {
            DataType.SmallInt => "SMALLINT",
            DataType.Int => "INTEGER",
            DataType.BigInt => "BIGINT",
            DataType.Decimal => "NUMERIC(18, 2)",
            DataType.Money => "MONEY",
            DataType.Float => "REAL",
            DataType.Double => "DOUBLE PRECISION",
            DataType.Char => "CHAR(255)",
            DataType.Varchar => "VARCHAR(255)",
            DataType.Text => "TEXT",
            DataType.NChar => "CHAR(255)",
            DataType.NVarchar => "VARCHAR(255)",
            DataType.Date => "DATE",
            DataType.Time => "TIME WITHOUT TIME ZONE",
            DataType.DateTime => "TIMESTAMP WITHOUT TIME ZONE",
            DataType.DateTime2 => "TIMESTAMP WITHOUT TIME ZONE",
            DataType.SmallDateTime => "TIMESTAMP WITHOUT TIME ZONE",
            DataType.Timestamp => "TIMESTAMP WITH TIME ZONE",
            DataType.Boolean => "BOOLEAN",
            DataType.Varbinary => "BYTEA",
            DataType.Binary => "BYTEA",
            DataType.Uuid => "UUID",
            DataType.Json => "JSONB",
            DataType.Xml => "XML",
            _ => throw new InvalidOperationException($"Unsupported data type: {type}")
        };
    }

    private string QuoteIdentifier(string identifier)
    {
        // PostgreSQL uses " for identifier quoting
        return $"\"{identifier}\"";
    }

    private string FormatValue(object? value)
    {
        if (value == null)
            return "NULL";
        if (value is string str)
            return $"'{str.Replace("'", "''")}'";
        if (value is bool b)
            return b ? "true" : "false";
        return value.ToString() ?? "NULL";
    }
}
