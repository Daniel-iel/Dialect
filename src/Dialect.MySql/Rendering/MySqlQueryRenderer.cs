namespace Dialect.MySql.Rendering;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using System.Text;

/// <summary>
/// MySQL query renderer.
/// Generates SQL for SELECT, INSERT, UPDATE, DELETE statements using MySQL syntax.
/// </summary>
public sealed class MySqlQueryRenderer : IQueryRenderer
{
    private int _paramCounter = 0;

    public CompiledQuery Render(SelectStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        _paramCounter = 0;
        var parameters = new Dictionary<string, object?>();
        var sql = RenderSelect(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(CompoundSelectStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        _paramCounter = 0;
        var parameters = new Dictionary<string, object?>();
        var sql = RenderCompoundSelect(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    public CompiledQuery Render(InsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        _paramCounter = 0;
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

        _paramCounter = 0;
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

        _paramCounter = 0;
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

        _paramCounter = 0;
        var parameters = new Dictionary<string, object?>();
        var sql = RenderUpsert(statement, dialect, parameters);

        return new CompiledQuery(sql, parameters);
    }

    private string RenderCompoundSelect(CompoundSelectStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();

        // Render the left SELECT wrapped in parentheses
        sb.Append("(");
        var leftRenderer = new MySqlQueryRenderer();
        var leftParams = new Dictionary<string, object?>();
        var leftSql = leftRenderer.RenderSelect(statement.Left, dialect, leftParams);
        
        // Merge left parameters
        foreach (var kvp in leftParams)
        {
            parameters[kvp.Key] = kvp.Value;
        }
        sb.Append(leftSql);
        sb.Append(")");

        // Set operator
        sb.Append(" ");
        sb.Append(statement.Operator switch
        {
            SetOperator.Union => "UNION",
            SetOperator.UnionAll => "UNION ALL",
            SetOperator.Intersect => "INTERSECT",
            SetOperator.Except => "EXCEPT",
            _ => throw new InvalidOperationException($"Set operator {statement.Operator} is not supported")
        });

        sb.Append(" ");

        // Render the right SELECT wrapped in parentheses
        sb.Append("(");
        var rightRenderer = new MySqlQueryRenderer();
        var rightParams = new Dictionary<string, object?>();
        var rightSql = rightRenderer.RenderSelect(statement.Right, dialect, rightParams);
        
        // Merge right parameters
        foreach (var kvp in rightParams)
        {
            parameters[kvp.Key] = kvp.Value;
        }
        sb.Append(rightSql);
        sb.Append(")");

        // If there's another compound operation chained, render it recursively
        if (statement.Next != null)
        {
            sb.Append(" ");
            var nextRenderer = new MySqlQueryRenderer();
            var nextParams = new Dictionary<string, object?>();
            var nextSql = nextRenderer.RenderCompoundSelect(statement.Next, dialect, nextParams);
            
            // Merge next parameters
            foreach (var kvp in nextParams)
            {
                parameters[kvp.Key] = kvp.Value;
            }
            // Note: nextSql already includes the parentheses, so we don't add them again
            sb.Append(nextSql);
        }

        return sb.ToString();
    }

    private string RenderSelect(SelectStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();

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
                var cteRenderer = new MySqlQueryRenderer();
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

        // SELECT [DISTINCT]
        sb.Append("SELECT");
        if (statement.IsDistinct)
            sb.Append(" DISTINCT");

        sb.Append(" ");

        // Columns and Window Functions
        var selectItems = new List<string>();

        // Regular columns
        foreach (var c in statement.Columns)
        {
            if (c.Name == "*")
                selectItems.Add("*");
            else if (c.IsRawExpression)
                selectItems.Add(c.Name);
            else
                selectItems.Add(QuoteIdentifier(c.Name, dialect));
        }

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
                var subqueryRenderer = new MySqlQueryRenderer();
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
                JoinType.Cross => "CROSS JOIN",
                _ => throw new InvalidOperationException($"Join type {join.Type} is not supported by MySQL")
            });

            sb.Append(" ");
            
            // Handle subquery or table name
            if (join.Table.SubquerySource != null)
            {
                // Render subquery wrapped in parentheses
                var subqueryRenderer = new MySqlQueryRenderer();
                var subqueryParams = new Dictionary<string, object?>();
                var subquerySql = subqueryRenderer.RenderSelect(join.Table.SubquerySource, dialect, subqueryParams);
                
                // Merge subquery parameters
                foreach (var kvp in subqueryParams)
                {
                    parameters[kvp.Key] = kvp.Value;
                }
                
                sb.Append("(").Append(subquerySql).Append(")");
            }
            else
            {
                sb.Append(QuoteIdentifier(join.Table.Name, dialect));
            }

            if (!string.IsNullOrEmpty(join.Table.Alias))
                sb.Append(" AS ").Append(QuoteIdentifier(join.Table.Alias, dialect));

            if (join.OnCondition != null)
            {
                sb.Append(" ON ");
                sb.Append(RenderWhereExpression(join.OnCondition, dialect, parameters));
            }
        }

        // WHERE
        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters));
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
            sb.Append(RenderWhereExpression(statement.Having, dialect, parameters));
        }

        // ORDER BY
        if (statement.OrderByClauses?.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.Append(string.Join(", ", statement.OrderByClauses.Select(o =>
                $"{QuoteIdentifier(o.Column.Name, dialect)} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}")));
        }

        // LIMIT / OFFSET (MySQL syntax, end of query)
        // Note: Older MySQL versions require LIMIT when using OFFSET
        // For MySQL 8.0.13+, OFFSET can work without LIMIT using special syntax
        if (statement.RowLimit?.Count.HasValue == true && statement.RowLimit.Count > 0)
        {
            sb.Append($" LIMIT {statement.RowLimit.Count}");
            if (statement.RowLimit?.Offset.HasValue == true && statement.RowLimit.Offset > 0)
                sb.Append($" OFFSET {statement.RowLimit.Offset}");
        }
        else if (statement.RowLimit?.Offset.HasValue == true && statement.RowLimit.Offset > 0)
        {
            // When only OFFSET is specified (no LIMIT), use a very large LIMIT for compatibility
            // This works with MySQL 8.0+
            sb.Append(" LIMIT 18446744073709551615");
            sb.Append($" OFFSET {statement.RowLimit.Offset}");
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
                        $"{QuoteIdentifier(o.Column.Name, dialect)} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}")));
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

        sb.Append("INSERT INTO ").Append(QuoteIdentifier(statement.Table.Name, dialect));
        sb.Append(" (").Append(string.Join(", ", statement.Columns.Select(c => QuoteIdentifier(c.Name, dialect)))).Append(")");

        if (statement.Values?.Count > 0)
        {
            sb.Append(" VALUES");
            var valueRows = new List<string>();

            foreach (var row in statement.Values)
            {
                var rowValues = new List<string>();
                foreach (var value in row)
                {
                    var paramName = $"p{++_paramCounter}";
                    parameters[paramName] = value;
                    rowValues.Add("?");
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

        sb.Append("UPDATE ").Append(QuoteIdentifier(statement.Table.Name, dialect));
        sb.Append(" SET ");

        var setClauses = new List<string>();
        foreach (var kvp in statement.SetClauses)
        {
            var paramName = $"p{++_paramCounter}";
            parameters[paramName] = kvp.Value;
            setClauses.Add($"{QuoteIdentifier(kvp.Key.Name, dialect)} = ?");
        }

        sb.Append(string.Join(", ", setClauses));

        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters));
        }

        return sb.ToString();
    }

    private string RenderDelete(DeleteStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var sb = new StringBuilder();

        sb.Append("DELETE FROM ").Append(QuoteIdentifier(statement.Table.Name, dialect));

        if (statement.Where != null)
        {
            sb.Append(" WHERE ");
            sb.Append(RenderWhereExpression(statement.Where, dialect, parameters));
        }

        return sb.ToString();
    }

    private string RenderWhereExpression(WhereExpression expr, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        return expr switch
        {
            AndNode and => $"({RenderWhereExpression(and.Left, dialect, parameters)} AND {RenderWhereExpression(and.Right, dialect, parameters)})",
            OrNode or => $"({RenderWhereExpression(or.Left, dialect, parameters)} OR {RenderWhereExpression(or.Right, dialect, parameters)})",
            ComparisonNode comp => RenderComparison(comp, dialect, parameters),
            InNode inNode => RenderInNode(inNode, dialect, parameters),
            RawNode raw => raw.SqlFragment,
            FunctionCallNode func => func.FunctionName,
            _ => throw new InvalidOperationException($"Unknown expression type: {expr.GetType().Name}")
        };
    }

    private string RenderComparison(ComparisonNode node, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var columnName = QuoteIdentifier(node.Column.Name, dialect);
        var paramName = $"p{++_paramCounter}";

        parameters[paramName] = node.Value;

        return node.Operator switch
        {
            ComparisonOperator.Equal => $"{columnName} = ?",
            ComparisonOperator.NotEqual => $"{columnName} <> ?",
            ComparisonOperator.LessThan => $"{columnName} < ?",
            ComparisonOperator.LessThanOrEqual => $"{columnName} <= ?",
            ComparisonOperator.GreaterThan => $"{columnName} > ?",
            ComparisonOperator.GreaterThanOrEqual => $"{columnName} >= ?",
            ComparisonOperator.Like => $"{columnName} LIKE ?",
            ComparisonOperator.NotLike => $"{columnName} NOT LIKE ?",
            ComparisonOperator.IsNull => $"{columnName} IS NULL",
            ComparisonOperator.IsNotNull => $"{columnName} IS NOT NULL",
            _ => throw new InvalidOperationException($"Unknown comparison operator: {node.Operator}")
        };
    }

    private string RenderInNode(InNode node, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        var columnName = QuoteIdentifier(node.Column.Name, dialect);

        // Handle subquery case: column IN (SELECT ...)
        if (node.SubquerySource != null)
        {
            var subqueryRenderer = new MySqlQueryRenderer();
            var subqueryParams = new Dictionary<string, object?>();
            var subquerySql = subqueryRenderer.RenderSelect(node.SubquerySource, dialect, subqueryParams);

            // Merge subquery parameters
            foreach (var kvp in subqueryParams)
            {
                parameters[kvp.Key] = kvp.Value;
            }

            return node.Negated
                ? $"{columnName} NOT IN ({subquerySql})"
                : $"{columnName} IN ({subquerySql})";
        }

        // Handle value list case: column IN (val1, val2, ...)
        var paramNames = new List<string>();

        foreach (var value in node.Values ?? Array.Empty<object?>())
        {
            var paramName = $"p{++_paramCounter}";
            parameters[paramName] = value;
            paramNames.Add("?");
        }

        var inList = string.Join(", ", paramNames);
        return node.Negated
            ? $"{columnName} NOT IN ({inList})"
            : $"{columnName} IN ({inList})";
    }

    private string RenderUpsert(UpsertStatement statement, ISqlDialect dialect, Dictionary<string, object?> parameters)
    {
        // UPSERT implementation for MySQL using ON DUPLICATE KEY UPDATE
        var sb = new StringBuilder();

        // INSERT INTO `table` (`col1`, `col2`, ...) VALUES (?, ?, ...)
        sb.Append("INSERT INTO ").Append(QuoteIdentifier(statement.Table.Name, dialect)).Append(" (");
        sb.Append(string.Join(", ", statement.Columns.Select(c => QuoteIdentifier(c.Name, dialect))));
        sb.Append(") VALUES (");

        var paramPlaceholders = new List<string>();
        for (int i = 0; i < statement.Columns.Count; i++)
        {
            _paramCounter++;
            parameters[$"?{_paramCounter}"] = statement.Values[i];
            paramPlaceholders.Add("?");
        }
        sb.Append(string.Join(", ", paramPlaceholders)).Append(")\n");

        // ON DUPLICATE KEY UPDATE `col1` = VALUES(`col1`), ...
        if (statement.ConflictClause?.UpdateClauses != null && statement.ConflictClause.UpdateClauses.Count > 0)
        {
            sb.Append("ON DUPLICATE KEY UPDATE ");
            var updateSets = new List<string>();
            foreach (var updateClause in statement.ConflictClause.UpdateClauses)
            {
                // MySQL allows VALUES(column) to reference the INSERT values
                updateSets.Add($"{QuoteIdentifier(updateClause.ColumnName, dialect)} = VALUES({QuoteIdentifier(updateClause.ColumnName, dialect)})");
            }
            sb.Append(string.Join(", ", updateSets));
        }

        return sb.ToString();
    }

    private static string QuoteIdentifier(string identifier, ISqlDialect dialect)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        // MySQL uses backticks for identifier quoting
        return $"`{identifier}`";
    }
}
