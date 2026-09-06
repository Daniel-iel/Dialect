namespace Dialect.SqlServer.Rendering;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using System.Text;

/// <summary>
/// SQL Server query renderer.
/// Generates T-SQL for SELECT, INSERT, UPDATE, DELETE statements.
/// </summary>
public sealed class SqlServerQueryRenderer : IQueryRenderer
{
    public CompiledQuery Render(SelectStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sql = RenderSelect(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(InsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sql = RenderInsert(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(UpdateStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sql = RenderUpdate(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(DeleteStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sql = RenderDelete(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(UpsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sql = RenderUpsert(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    private string RenderSelect(SelectStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();
        var paramCounter = 1;

        // WITH clause (CTEs)
        if (statement.WithClauses.Count > 0)
        {
            sb.Append("WITH ");
            var cteParts = new List<string>();
            foreach (var cte in statement.WithClauses)
            {
                var cteName = QuoteIdentifier(cte.Name, dialect);
                var columnSpec = cte.ColumnNames?.Count > 0
                    ? $"({string.Join(", ", cte.ColumnNames.Select(c => QuoteIdentifier(c, dialect)))})"
                    : "";

                // Render the CTE's SELECT statement
                var cteRenderer = new SqlServerQueryRenderer();
                var cteParams = new Dictionary<string, object?>();
                var cteSql = cteRenderer.RenderSelect(cte.Query, dialect, cteParams);

                // Merge CTE parameters into main parameters
                foreach (var kvp in cteParams)
                {
                    parameters[kvp.Key] = kvp.Value;
                }

                cteParts.Add($"{cteName}{columnSpec} AS ({cteSql})");
            }
            sb.Append(string.Join(", ", cteParts));
            sb.Append(" ");
        }

        // SELECT [DISTINCT] [TOP n]
        sb.Append("SELECT");
        if (statement.IsDistinct)
            sb.Append(" DISTINCT");

        if (statement.RowLimit?.Offset == null && statement.RowLimit?.Count > 0)
        {
            // Use TOP syntax when no OFFSET (or OFFSET is 0)
            sb.Append($" TOP {statement.RowLimit.Count}");
            if (statement.RowLimit.WithTies)
                sb.Append(" WITH TIES");
        }

        sb.Append(" ");

        // Columns and Window Functions
        var selectItems = new List<string>();

        // Regular columns
        selectItems.AddRange(statement.Columns.Select(c => QuoteIdentifier(c.Name, dialect)));

        // Window functions
        selectItems.AddRange(statement.WindowFunctions.Select(wf => RenderWindowFunction(wf, dialect)));

        sb.Append(string.Join(", ", selectItems));

        // FROM
        if (statement.From != null)
        {
            sb.Append(" FROM ");

            if (statement.From.SubquerySource != null)
            {
                // Render subquery
                var subqueryRenderer = new SqlServerQueryRenderer();
                var subqueryParams = new Dictionary<string, object?>();
                var subquerySql = subqueryRenderer.RenderSelect(statement.From.SubquerySource, dialect, subqueryParams);

                // Merge subquery parameters
                foreach (var kvp in subqueryParams)
                {
                    parameters[kvp.Key] = kvp.Value;
                }

                sb.Append($"({subquerySql})");
            }
            else
            {
                sb.Append(QuoteIdentifier(statement.From.Name, dialect));
            }

            if (!string.IsNullOrEmpty(statement.From.Alias))
                sb.Append(" AS ").Append(QuoteIdentifier(statement.From.Alias, dialect));
        }

        // JOINs
        foreach (var join in statement.Joins)
        {
            sb.Append(" ");
            sb.Append(join.Type switch
            {
                JoinType.Inner => "INNER JOIN",
                JoinType.Left => "LEFT JOIN",
                JoinType.Right => "RIGHT JOIN",
                JoinType.Full => "FULL OUTER JOIN",
                JoinType.Cross => "CROSS JOIN",
                _ => throw new InvalidOperationException($"Unknown join type: {join.Type}")
            });

            sb.Append(" ").Append(QuoteIdentifier(join.Table.Name, dialect));
            if (!string.IsNullOrEmpty(join.Table.Alias))
                sb.Append(" AS ").Append(QuoteIdentifier(join.Table.Alias, dialect));

            if (join.OnCondition != null)
            {
                sb.Append(" ON ");
                sb.Append(RenderWhereExpression(join.OnCondition, dialect, parameters, ref paramCounter));
            }
        }

        // WHERE
        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters, ref paramCounter));
        }

        // GROUP BY
        if (statement.GroupByColumns?.Count > 0)
        {
            sb.Append(" GROUP BY ");
            sb.Append(string.Join(", ", statement.GroupByColumns.Select(c => QuoteIdentifier(c.Name, dialect))));
        }

        // HAVING
        if (statement.Having != null)
        {
            sb.Append(" HAVING ");
            sb.Append(RenderWhereExpression(statement.Having, dialect, parameters, ref paramCounter));
        }

        // ORDER BY (required for OFFSET/FETCH, or when we have ORDER BY)
        if (statement.OrderByClauses?.Count > 0 || statement.RowLimit?.Offset > 0)
        {
            sb.Append(" ORDER BY ");
            if (statement.OrderByClauses?.Count > 0)
            {
                sb.Append(string.Join(", ", statement.OrderByClauses.Select(o =>
                    $"{QuoteIdentifier(o.Column.Name, dialect)} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}")));
            }
            else
            {
                // OFFSET requires ORDER BY; use minimal order by
                sb.Append("(SELECT NULL)");
            }

            // OFFSET/FETCH NEXT - only when OFFSET is > 0
            if (statement.RowLimit?.Offset > 0)
            {
                sb.Append($" OFFSET {statement.RowLimit.Offset} ROWS");
                if (statement.RowLimit?.Count > 0)
                    sb.Append($" FETCH NEXT {statement.RowLimit.Count} ROWS ONLY");
            }
        }

        return sb.ToString();
    }

    private string RenderWindowFunction(WindowFunction windowFunction, ISqlDialect dialect)
    {
        var sb = new StringBuilder();

        // Function name and arguments
        sb.Append(windowFunction.FunctionName).Append("(");
        if (windowFunction.Args?.Count > 0)
        {
            sb.Append(string.Join(", ", windowFunction.Args.Select(a => QuoteIdentifier(a, dialect))));
        }
        sb.Append(")");

        // OVER clause
        if (windowFunction.Over != null)
        {
            sb.Append(" OVER (");

            var overParts = new List<string>();

            // PARTITION BY
            if (windowFunction.Over.PartitionByColumns?.Count > 0)
            {
                overParts.Add("PARTITION BY " + string.Join(", ",
                    windowFunction.Over.PartitionByColumns.Select(c => QuoteIdentifier(c, dialect))));
            }

            // ORDER BY
            if (windowFunction.Over.OrderByItems?.Count > 0)
            {
                overParts.Add("ORDER BY " + string.Join(", ",
                    windowFunction.Over.OrderByItems.Select(o =>
                        $"{QuoteIdentifier(o.Column.Name, dialect)} {o.Direction}")));
            }

            // Frame specification (ROWS/RANGE)
            if (windowFunction.Over.Frame != null)
            {
                overParts.Add(windowFunction.Over.Frame.ToSql());
            }

            sb.Append(string.Join(" ", overParts));
            sb.Append(")");
        }

        // Alias
        if (!string.IsNullOrEmpty(windowFunction.Alias))
        {
            sb.Append(" AS ").Append(QuoteIdentifier(windowFunction.Alias, dialect));
        }

        return sb.ToString();
    }

    private string RenderInsert(InsertStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();
        var paramCounter = 1;

        sb.Append("INSERT INTO ").Append(QuoteIdentifier(statement.Table.Name, dialect));
        sb.Append(" (").Append(string.Join(", ", statement.Columns.Select(c => QuoteIdentifier(c.Name, dialect)))).Append(")");

        if (statement.SelectSource != null)
        {
            sb.Append(" ");
            // Render the SELECT source (simplified for now, would need nested handling)
            throw new NotImplementedException("INSERT FROM SELECT not yet implemented");
        }
        else if (statement.Values?.Count > 0)
        {
            sb.Append(" VALUES");
            var valueRows = new List<string>();

            foreach (var row in statement.Values)
            {
                var rowValues = new List<string>();
                foreach (var value in row)
                {
                    var paramName = $"{dialect.ParameterPrefix}p{paramCounter++}";
                    parameters[paramName.TrimStart('@')] = value;
                    rowValues.Add(paramName);
                }
                valueRows.Add($"({string.Join(", ", rowValues)})");
            }

            sb.Append(" ").Append(string.Join(", ", valueRows));
        }

        return sb.ToString();
    }

    private string RenderUpdate(UpdateStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();
        var paramCounter = 1;

        sb.Append("UPDATE ").Append(QuoteIdentifier(statement.Table.Name, dialect));
        sb.Append(" SET ");

        var setClauses = new List<string>();
        foreach (var kvp in statement.SetClauses)
        {
            var paramName = $"{dialect.ParameterPrefix}p{paramCounter++}";
            parameters[paramName.TrimStart('@')] = kvp.Value;
            setClauses.Add($"{QuoteIdentifier(kvp.Key.Name, dialect)} = {paramName}");
        }

        sb.Append(string.Join(", ", setClauses));

        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters, ref paramCounter));
        }

        return sb.ToString();
    }

    private string RenderDelete(DeleteStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();
        var paramCounter = 1;

        sb.Append("DELETE FROM ").Append(QuoteIdentifier(statement.Table.Name, dialect));

        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters, ref paramCounter));
        }

        return sb.ToString();
    }

    private string RenderWhereExpression(WhereExpression expr, ISqlDialect dialect, Dictionary<string, object?> parameters, ref int paramCounter)
    {
        return expr switch
        {
            AndNode and => $"({RenderWhereExpression(and.Left, dialect, parameters, ref paramCounter)} AND {RenderWhereExpression(and.Right, dialect, parameters, ref paramCounter)})",
            OrNode or => $"({RenderWhereExpression(or.Left, dialect, parameters, ref paramCounter)} OR {RenderWhereExpression(or.Right, dialect, parameters, ref paramCounter)})",
            ComparisonNode comp => RenderComparison(comp, dialect, parameters, ref paramCounter),
            InNode inNode => RenderInNode(inNode, dialect, parameters, ref paramCounter),
            RawNode raw => raw.SqlFragment, // TODO: Validate and merge parameters
            FunctionCallNode func => func.FunctionName, // TODO: Implement function rendering
            _ => throw new InvalidOperationException($"Unknown expression type: {expr.GetType().Name}")
        };
    }

    private string RenderComparison(ComparisonNode node, ISqlDialect dialect, Dictionary<string, object?> parameters, ref int paramCounter)
    {
        var columnName = QuoteIdentifier(node.Column.Name, dialect);
        var paramName = $"{dialect.ParameterPrefix}p{paramCounter++}";

        parameters[paramName.TrimStart('@')] = node.Value;

        return node.Operator switch
        {
            ComparisonOperator.Equal => $"{columnName} = {paramName}",
            ComparisonOperator.NotEqual => $"{columnName} <> {paramName}",
            ComparisonOperator.LessThan => $"{columnName} < {paramName}",
            ComparisonOperator.LessThanOrEqual => $"{columnName} <= {paramName}",
            ComparisonOperator.GreaterThan => $"{columnName} > {paramName}",
            ComparisonOperator.GreaterThanOrEqual => $"{columnName} >= {paramName}",
            ComparisonOperator.Like => $"{columnName} LIKE {paramName}",
            ComparisonOperator.NotLike => $"{columnName} NOT LIKE {paramName}",
            ComparisonOperator.IsNull => $"{columnName} IS NULL",
            ComparisonOperator.IsNotNull => $"{columnName} IS NOT NULL",
            _ => throw new InvalidOperationException($"Unknown comparison operator: {node.Operator}")
        };
    }

    private string RenderInNode(InNode node, ISqlDialect dialect, Dictionary<string, object?> parameters, ref int paramCounter)
    {
        var columnName = QuoteIdentifier(node.Column.Name, dialect);
        var paramNames = new List<string>();

        foreach (var value in node.Values)
        {
            var paramName = $"{dialect.ParameterPrefix}p{paramCounter++}";
            parameters[paramName.TrimStart('@')] = value;
            paramNames.Add(paramName);
        }

        var inList = string.Join(", ", paramNames);
        return node.Negated
            ? $"{columnName} NOT IN ({inList})"
            : $"{columnName} IN ({inList})";
    }

    private string RenderUpsert(UpsertStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        // UPSERT implementation for SQL Server using MERGE
        var sb = new StringBuilder();
        var paramCounter = 1;

        // MERGE INTO [Table] AS target
        sb.Append("MERGE INTO ").Append(QuoteIdentifier(statement.Table.Name, dialect)).Append(" AS target\n");

        // USING (SELECT @p1 AS Col1, @p2 AS Col2, ...) AS source
        sb.Append("USING (SELECT ");
        var sourceSelects = new List<string>();
        for (int i = 0; i < statement.Columns.Count; i++)
        {
            var paramName = $"@p{paramCounter}";
            parameters[paramName] = statement.Values[i];
            sourceSelects.Add($"{paramName} AS {QuoteIdentifier(statement.Columns[i].Name, dialect)}");
            paramCounter++;
        }
        sb.Append(string.Join(", ", sourceSelects)).Append(") AS source\n");

        // ON target.ConflictCol1 = source.ConflictCol1 AND target.ConflictCol2 = source.ConflictCol2
        if (statement.ConflictClause?.ConflictColumns != null && statement.ConflictClause.ConflictColumns.Count > 0)
        {
            sb.Append("ON ");
            var onConditions = new List<string>();
            foreach (var conflictCol in statement.ConflictClause.ConflictColumns)
            {
                onConditions.Add($"target.{QuoteIdentifier(conflictCol, dialect)} = source.{QuoteIdentifier(conflictCol, dialect)}");
            }
            sb.Append(string.Join(" AND ", onConditions)).Append("\n");
        }

        // WHEN MATCHED THEN UPDATE SET Col1 = source.Col1, ...
        if (statement.ConflictClause?.UpdateClauses != null && statement.ConflictClause.UpdateClauses.Count > 0)
        {
            sb.Append("WHEN MATCHED THEN UPDATE SET ");
            var updateSets = new List<string>();
            foreach (var updateClause in statement.ConflictClause.UpdateClauses)
            {
                var paramName = $"@p{paramCounter}";
                parameters[paramName] = updateClause.Value;
                updateSets.Add($"{QuoteIdentifier(updateClause.ColumnName, dialect)} = {paramName}");
                paramCounter++;
            }
            sb.Append(string.Join(", ", updateSets)).Append("\n");
        }

        // WHEN NOT MATCHED BY TARGET THEN INSERT (Col1, Col2, ...) VALUES (source.Col1, source.Col2, ...)
        sb.Append("WHEN NOT MATCHED BY TARGET THEN INSERT (");
        sb.Append(string.Join(", ", statement.Columns.Select(c => QuoteIdentifier(c.Name, dialect))));
        sb.Append(") VALUES (");
        var sourceRefs = statement.Columns.Select(c => $"source.{QuoteIdentifier(c.Name, dialect)}");
        sb.Append(string.Join(", ", sourceRefs)).Append(");");

        return sb.ToString();
    }

    private static string QuoteIdentifier(string identifier, ISqlDialect dialect)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        // SQL Server uses [ and ] for identifier quoting
        return $"[{identifier}]";
    }
}
