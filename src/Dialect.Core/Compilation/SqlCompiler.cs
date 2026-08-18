namespace Dialect.Core.Compilation;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using System.Text.RegularExpressions;

/// <summary>
/// Handles compilation of AST nodes to SQL text with parameter validation.
/// </summary>
public static class SqlCompiler
{
    /// <summary>
    /// Regex for valid SQL identifiers (tables, columns, aliases).
    /// Allows alphanumeric, underscore, starting with letter or underscore.
    /// </summary>
    private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Compiles a SELECT statement to SQL.
    /// </summary>
    public static CompiledQuery Compile(this SelectStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        ValidateStatement(statement, dialect);
        
        var renderer = dialect.CreateQueryRenderer();
        return renderer.Render(statement, dialect);
    }

    /// <summary>
    /// Compiles an INSERT statement to SQL.
    /// </summary>
    public static CompiledQuery Compile(this InsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        ValidateStatement(statement, dialect);
        
        var renderer = dialect.CreateQueryRenderer();
        return renderer.Render(statement, dialect);
    }

    /// <summary>
    /// Compiles an UPDATE statement to SQL.
    /// </summary>
    public static CompiledQuery Compile(this UpdateStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        ValidateStatement(statement, dialect);
        
        var renderer = dialect.CreateQueryRenderer();
        return renderer.Render(statement, dialect);
    }

    /// <summary>
    /// Compiles a DELETE statement to SQL.
    /// </summary>
    public static CompiledQuery Compile(this DeleteStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        ValidateStatement(statement, dialect);
        
        var renderer = dialect.CreateQueryRenderer();
        return renderer.Render(statement, dialect);
    }

    /// <summary>
    /// Compiles a routine call to SQL.
    /// </summary>
    public static CompiledQuery Compile(this RoutineCall routine, ISqlDialect dialect)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        ValidateRoutine(routine, dialect);
        
        var renderer = dialect.CreateRoutineRenderer();
        return renderer.Render(routine, dialect);
    }

    /// <summary>
    /// Compiles an UPSERT statement to SQL.
    /// </summary>
    public static CompiledQuery Compile(this UpsertStatement statement, ISqlDialect dialect)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        
        if (!dialect.Supports(SqlFeature.Upsert))
            throw new SqlCompilationException(
                $"UPSERT is not supported by {dialect.GetType().Name}");
        
        ValidateUpsertStatement(statement, dialect);
        
        var renderer = dialect.CreateQueryRenderer();
        return renderer.Render(statement, dialect);
    }

    /// <summary>
    /// Validates a SELECT statement for correctness and dialect support.
    /// </summary>
    private static void ValidateStatement(SelectStatement statement, ISqlDialect dialect)
    {
        // Check for FULL JOIN support
        foreach (var join in statement.Joins)
        {
            if (join.Type == JoinType.Full && !dialect.Supports(SqlFeature.FullJoin))
                throw new SqlCompilationException(
                    $"FULL OUTER JOIN is not supported by {dialect.GetType().Name}. " +
                    "Consider using UNION or changing your join strategy.");
        }

        // Validate identifiers
        if (statement.From?.SubquerySource == null)
        {
            ValidateIdentifier(statement.From?.Name);
        }
        else
        {
            // Validate subquery
            if (string.IsNullOrWhiteSpace(statement.From.Alias))
                throw new SqlCompilationException("Subqueries in FROM clause must have an alias.");
            
            // Recursively validate and compile the subquery
            ValidateStatement(statement.From.SubquerySource, dialect);
        }
        
        ValidateIdentifier(statement.From?.Alias);
        
        foreach (var join in statement.Joins)
        {
            if (join.Table.SubquerySource == null)
            {
                ValidateIdentifier(join.Table.Name);
            }
            else
            {
                // Validate subquery in JOIN
                if (string.IsNullOrWhiteSpace(join.Table.Alias))
                    throw new SqlCompilationException("Subqueries in JOIN clauses must have an alias.");
                
                ValidateStatement(join.Table.SubquerySource, dialect);
            }
            ValidateIdentifier(join.Table.Alias);
        }

        // Validate OFFSET with ORDER BY requirement
        if (statement.RowLimit?.Offset.HasValue == true && statement.RowLimit.Offset > 0)
        {
            if (statement.OrderByClauses == null || statement.OrderByClauses.Count == 0)
                throw new SqlCompilationException(
                    "OFFSET requires ORDER BY clause. Cannot page without ordering.");
        }

        // Validate columns in GROUP BY
        if (statement.GroupByColumns?.Count > 0 && statement.GroupByColumns.Count > 0)
        {
            ValidateIdentifier(statement.GroupByColumns[0].Name);
        }
    }

    /// <summary>
    /// Validates an INSERT statement for correctness and dialect support.
    /// </summary>
    private static void ValidateStatement(InsertStatement statement, ISqlDialect dialect)
    {
        ValidateIdentifier(statement.Table.Name);
        ValidateIdentifier(statement.Table.Schema);

        if (statement.Columns.Count == 0)
            throw new SqlCompilationException("INSERT must specify at least one column.");
    }

    /// <summary>
    /// Validates an UPDATE statement for correctness and dialect support.
    /// </summary>
    private static void ValidateStatement(UpdateStatement statement, ISqlDialect dialect)
    {
        ValidateIdentifier(statement.Table.Name);
        ValidateIdentifier(statement.Table.Schema);

        if (statement.Where == null && !statement.AllowFullTableUpdate)
            throw new SqlCompilationException(
                "UPDATE without WHERE is dangerous. Use AllowFullTableUpdate() if intentional.");
    }

    /// <summary>
    /// Validates a DELETE statement for correctness and dialect support.
    /// </summary>
    private static void ValidateStatement(DeleteStatement statement, ISqlDialect dialect)
    {
        ValidateIdentifier(statement.Table.Name);
        ValidateIdentifier(statement.Table.Schema);

        if (statement.Where == null && !statement.AllowFullTableDelete)
            throw new SqlCompilationException(
                "DELETE without WHERE is dangerous. Use AllowFullTableOperation() if intentional.");
    }

    /// <summary>
    /// Validates a routine call for correctness and dialect support.
    /// </summary>
    private static void ValidateRoutine(RoutineCall routine, ISqlDialect dialect)
    {
        if (routine.Kind == RoutineKind.Procedure && !dialect.Supports(SqlFeature.StoredProcedures))
            throw new SqlCompilationException(
                $"Stored procedures are not supported by {dialect.GetType().Name}.");

        if (routine.Kind == RoutineKind.Function && !dialect.Supports(SqlFeature.Functions))
            throw new SqlCompilationException(
                $"Functions are not supported by {dialect.GetType().Name}.");

        ValidateIdentifier(routine.Name);
        ValidateIdentifier(routine.Schema);
        ValidateIdentifier(routine.Package);
    }

    private static void ValidateUpsertStatement(UpsertStatement statement, ISqlDialect dialect)
    {
        if (statement.Table == null)
            throw new SqlCompilationException("UPSERT must specify a target table");
        
        ValidateIdentifier(statement.Table.Name);
        ValidateIdentifier(statement.Table.Alias);
        
        if (statement.Columns.Count == 0)
            throw new SqlCompilationException("UPSERT must specify at least one column");
        
        foreach (var column in statement.Columns)
        {
            ValidateIdentifier(column.Name);
        }
        
        if (statement.Values == null || statement.Values.Count == 0)
            throw new SqlCompilationException("UPSERT must specify values");
        
        if (statement.Values.Count != statement.Columns.Count)
            throw new SqlCompilationException(
                $"UPSERT column count ({statement.Columns.Count}) must match value count ({statement.Values.Count})");
        
        if (statement.ConflictClause != null)
        {
            if (statement.ConflictClause.UpdateClauses == null || statement.ConflictClause.UpdateClauses.Count == 0)
                throw new SqlCompilationException("UPSERT conflict clause must specify at least one update column");
            
            foreach (var updateClause in statement.ConflictClause.UpdateClauses)
            {
                ValidateIdentifier(updateClause.ColumnName);
            }
        }
    }

    /// <summary>
    /// Validates that an identifier conforms to SQL naming rules.
    /// Throws if the identifier contains invalid characters.
    /// </summary>
    private static void ValidateIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        if (!IdentifierPattern.IsMatch(identifier))
            throw new SqlCompilationException(
                $"Invalid SQL identifier '{identifier}'. Identifiers must start with a letter or underscore " +
                "and contain only alphanumeric characters and underscores.");
    }
}

/// <summary>
/// Exception thrown when SQL compilation fails due to validation errors.
/// </summary>
public sealed class SqlCompilationException : Exception
{
    public SqlCompilationException(string message) : base(message) { }
    public SqlCompilationException(string message, Exception innerException) 
        : base(message, innerException) { }
}
