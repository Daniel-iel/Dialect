using System;
using System.Collections.Generic;
using System.Linq;
using Dialect.Core.AST;

namespace Dialect.Core.Parsing
{
    /// <summary>
    /// SQL parser adapter using hybrid regex/manual parsing.
    /// Simplified implementation for common SQL patterns.
    /// Full Antlr4 grammar integration will follow in next phase.
    /// </summary>
    public class AntlrSqlParser : ISqlParser
    {
        /// <summary>
        /// Parse SQL string into SELECT statement.
        /// Returns null for non-SELECT statements.
        /// </summary>
        public SelectStatement? Parse(string sql, SqlProvider dialect)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return null;

            try
            {
                var normalized = NormalizeSql(sql.Trim());

                if (normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    return ParseSelect(sql.Trim(), dialect);

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string NormalizeSql(string sql)
        {
            while (sql.Contains("  "))
                sql = sql.Replace("  ", " ");
            return sql.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        private SelectStatement? ParseSelect(string sql, SqlProvider dialect)
        {
            try
            {
                var isDistinct = sql.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase);

                // Extract columns
                var columns = ExtractSelectColumns(sql);
                if (columns == null || columns.Count == 0)
                    return null;

                // Extract FROM table
                TableReference? tableRef = null;
                var fromClause = ExtractFromClause(sql);
                if (fromClause != null)
                    tableRef = new TableReference(Name: fromClause.Trim());

                // Extract GROUP BY
                IReadOnlyList<Column>? groupByColumns = null;
                var groupByClause = ExtractGroupByClause(sql);
                if (groupByClause != null)
                {
                    groupByColumns = groupByClause.Split(',')
                        .Select(c => new Column(Name: c.Trim()))
                        .ToList();
                }

                // Extract ORDER BY
                IReadOnlyList<OrderByClause>? orderByList = null;
                var orderByClause = ExtractOrderByClause(sql);
                if (orderByClause != null)
                    orderByList = ParseOrderByClause(orderByClause);

                // Extract LIMIT/OFFSET
                var rowLimit = ExtractRowLimit(sql);

                return new SelectStatement(
                    Columns: columns,
                    From: tableRef,
                    GroupByColumns: groupByColumns,
                    OrderByClauses: orderByList,
                    RowLimit: rowLimit,
                    IsDistinct: isDistinct);
            }
            catch
            {
                return null;
            }
        }

        private List<Column>? ExtractSelectColumns(string sql)
        {
            var selectIndex = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            if (selectIndex == -1) return null;

            var afterSelect = sql.Substring(selectIndex + 6).Trim();
            var endKeywords = new[] { "FROM", "WHERE", "GROUP", "ORDER", "LIMIT", "OFFSET" };
            var endIndex = afterSelect.Length;

            foreach (var keyword in endKeywords)
            {
                var idx = afterSelect.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx != -1 && idx < endIndex)
                    endIndex = idx;
            }

            var selectList = afterSelect.Substring(0, endIndex).Trim();

            // Remove leading DISTINCT keyword from select list so column names are clean
            if (selectList.StartsWith("DISTINCT ", StringComparison.OrdinalIgnoreCase))
                selectList = selectList.Substring(8).Trim();
            var columns = new List<Column>();

            foreach (var part in SplitByComma(selectList))
            {
                var trimmed = part.Trim();
                if (trimmed == "*")
                    columns.Add(new Column(Name: "*"));
                else if (trimmed.EndsWith(".*"))
                    columns.Add(new Column(Name: "*", TableAlias: trimmed.Substring(0, trimmed.Length - 2)));
                else
                    columns.Add(new Column(Name: trimmed));
            }

            return columns.Count > 0 ? columns : null;
        }

        private string? ExtractFromClause(string sql)
        {
            var fromIndex = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
            if (fromIndex == -1) return null;

            var afterFrom = sql.Substring(fromIndex + 4).Trim();
            var endKeywords = new[] { "WHERE", "GROUP", "ORDER", "LIMIT", "OFFSET", "JOIN" };
            var endIndex = afterFrom.Length;

            foreach (var keyword in endKeywords)
            {
                var idx = afterFrom.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx != -1 && idx < endIndex)
                    endIndex = idx;
            }

            var rawCandidate = afterFrom.Substring(0, endIndex).Trim();

            // Fallback: if the extraction returned an empty string (malformed SQL), try to grab the first token
            // This handles cases like "FROM (SELECT ...) AS x" or unexpected whitespace
            if (string.IsNullOrWhiteSpace(rawCandidate))
            {
                var tokenMatch = System.Text.RegularExpressions.Regex.Match(afterFrom, "^\\s*([^\\s,()]+)");
                if (tokenMatch.Success)
                    return tokenMatch.Groups[1].Value.Trim();
                return null;
            }

            return rawCandidate;
        }

        private string? ExtractGroupByClause(string sql)
        {
            var groupIndex = sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
            if (groupIndex == -1) return null;

            var afterGroup = sql.Substring(groupIndex + 8).Trim();
            var endKeywords = new[] { "HAVING", "ORDER", "LIMIT", "OFFSET" };
            var endIndex = afterGroup.Length;

            foreach (var keyword in endKeywords)
            {
                var idx = afterGroup.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx != -1 && idx < endIndex)
                    endIndex = idx;
            }

            return afterGroup.Substring(0, endIndex).Trim();
        }

        private string? ExtractOrderByClause(string sql)
        {
            var orderIndex = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
            if (orderIndex == -1) return null;

            var afterOrder = sql.Substring(orderIndex + 8).Trim();
            var endKeywords = new[] { "LIMIT", "OFFSET", "FETCH" };
            var endIndex = afterOrder.Length;

            foreach (var keyword in endKeywords)
            {
                var idx = afterOrder.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx != -1 && idx < endIndex)
                    endIndex = idx;
            }

            return afterOrder.Substring(0, endIndex).Trim();
        }

        private RowLimit? ExtractRowLimit(string sql)
        {
            // TOP (SQL Server)
            var topIndex = sql.IndexOf("TOP", StringComparison.OrdinalIgnoreCase);
            if (topIndex != -1)
            {
                var afterTop = sql.Substring(topIndex + 3).Trim();
                var parts = afterTop.Split(new[] { ' ', ',', ')', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out var topCount))
                    return new RowLimit(Count: topCount);
            }

            // LIMIT (MySQL, PostgreSQL)
            var limitIndex = sql.IndexOf("LIMIT", StringComparison.OrdinalIgnoreCase);
            if (limitIndex != -1)
            {
                var afterLimit = sql.Substring(limitIndex + 5).Trim();
                var parts = afterLimit.Split(new[] { ' ', ',', ')', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out var limitCount))
                    return new RowLimit(Count: limitCount);
            }

            return null;
        }

        private List<OrderByClause>? ParseOrderByClause(string orderByClause)
        {
            if (string.IsNullOrEmpty(orderByClause)) return null;

            var items = SplitByComma(orderByClause);
            var orderByList = new List<OrderByClause>();

            foreach (var item in items)
            {
                var trimmed = item.Trim();
                var direction = SortDirection.Ascending;

                if (trimmed.EndsWith("DESC", StringComparison.OrdinalIgnoreCase))
                {
                    direction = SortDirection.Descending;
                    trimmed = trimmed.Substring(0, trimmed.Length - 4).Trim();
                }
                else if (trimmed.EndsWith("ASC", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 3).Trim();
                }

                orderByList.Add(new OrderByClause(
                    Column: new Column(Name: trimmed),
                    Direction: direction));
            }

            return orderByList.Count > 0 ? orderByList : null;
        }

        private List<string> SplitByComma(string input)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var depth = 0;

            foreach (var ch in input)
            {
                if ((ch == '\'' || ch == '"') && (current.Length == 0 || current[current.Length - 1] != '\\'))
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = ch;
                    }
                    else if (ch == quoteChar)
                    {
                        inQuotes = false;
                    }
                }

                if (!inQuotes)
                {
                    if (ch == '(') depth++;
                    if (ch == ')') depth--;
                    if (ch == ',' && depth == 0)
                    {
                        result.Add(current.Trim());
                        current = "";
                        continue;
                    }
                }

                current += ch;
            }

            if (!string.IsNullOrWhiteSpace(current))
                result.Add(current.Trim());

            return result;
        }
    }

    /// <summary>
    /// Interface for SQL parsers.
    /// </summary>
    public interface ISqlParser
    {
        SelectStatement? Parse(string sql, SqlProvider dialect);
    }
}
