namespace Dialect.MySql.Rendering;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using System.Text;

/// <summary>
/// MySQL routine (stored procedure / function) renderer.
/// </summary>
public sealed class MySqlRoutineRenderer : IRoutineRenderer
{
    public CompiledQuery Render(RoutineCall routine, ISqlDialect dialect)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sb = new StringBuilder();

        // MySQL uses CALL for procedures
        sb.Append("CALL ");

        if (!string.IsNullOrEmpty(routine.Schema))
            sb.Append($"`{routine.Schema}`.");

        sb.Append($"`{routine.Name}`");

        var paramList = new List<string>();
        var paramCounter = 0;

        foreach (var param in routine.Parameters)
        {
            if (param.Value != null)
            {
                var paramName = $"p{++paramCounter}";
                parameters[paramName] = param.Value;
                paramList.Add("?");
            }
            else if (param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.InputOutput)
            {
                paramList.Add("@" + param.Name);
            }
        }

        sb.Append("(").Append(string.Join(", ", paramList)).Append(")");

        return new CompiledQuery(sb.ToString(), parameters);
    }
}
