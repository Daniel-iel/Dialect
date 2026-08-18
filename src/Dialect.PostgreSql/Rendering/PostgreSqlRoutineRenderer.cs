namespace Dialect.PostgreSql.Rendering;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using System.Text;

/// <summary>
/// PostgreSQL routine (function) renderer.
/// PostgreSQL doesn't have stored procedures in the traditional sense; functions are used instead.
/// </summary>
public sealed class PostgreSqlRoutineRenderer : IRoutineRenderer
{
    public CompiledQuery Render(RoutineCall routine, ISqlDialect dialect)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sb = new StringBuilder();

        // PostgreSQL uses SELECT for functions, CALL for procedures (9.1+)
        sb.Append(routine.Kind == RoutineKind.Procedure ? "CALL " : "SELECT ");

        if (!string.IsNullOrEmpty(routine.Schema))
            sb.Append($"\"{routine.Schema}\".");

        sb.Append($"\"{routine.Name}\"");

        var paramList = new List<string>();
        var paramCounter = 0;

        foreach (var param in routine.Parameters)
        {
            if (param.Value != null)
            {
                var paramName = $"p{++paramCounter}";
                parameters[paramName] = param.Value;
                paramList.Add($"${paramCounter}");
            }
        }

        sb.Append("(").Append(string.Join(", ", paramList)).Append(")");

        return new CompiledQuery(sb.ToString(), parameters);
    }
}
