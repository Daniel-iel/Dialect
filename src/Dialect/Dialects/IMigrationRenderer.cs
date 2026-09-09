namespace Dialect.Core.Dialects;

using Dialect.Core.AST;
using Dialect.Core.AST.Migration;

/// <summary>
/// Defines the contract for rendering migration steps to SQL.
/// </summary>
public interface IMigrationRenderer
{
    /// <summary>
    /// Renders a migration to SQL script.
    /// </summary>
    CompiledQuery Render(Migration migration, ISqlDialect dialect);

    /// <summary>
    /// Renders a single migration step to SQL.
    /// </summary>
    string RenderStep(MigrationStep step, ISqlDialect dialect);
}
