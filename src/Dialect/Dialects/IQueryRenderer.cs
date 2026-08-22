namespace Dialect.Core.Dialects;

using Dialect.Core.AST;

/// <summary>
/// Defines the contract for rendering query statements to SQL.
/// </summary>
public interface IQueryRenderer
{
    /// <summary>
    /// Renders a SELECT statement to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(SelectStatement statement, ISqlDialect dialect);
    
    /// <summary>
    /// Renders an INSERT statement to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(InsertStatement statement, ISqlDialect dialect);
    
    /// <summary>
    /// Renders an UPDATE statement to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(UpdateStatement statement, ISqlDialect dialect);
    
    /// <summary>
    /// Renders a DELETE statement to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(DeleteStatement statement, ISqlDialect dialect);
    
    /// <summary>
    /// Renders an UPSERT statement to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(UpsertStatement statement, ISqlDialect dialect);
}

/// <summary>
/// Defines the contract for rendering routine calls (procedures/functions).
/// </summary>
public interface IRoutineRenderer
{
    /// <summary>
    /// Renders a procedure or function call to SQL text with parameters.
    /// </summary>
    CompiledQuery Render(RoutineCall routine, ISqlDialect dialect);
}
