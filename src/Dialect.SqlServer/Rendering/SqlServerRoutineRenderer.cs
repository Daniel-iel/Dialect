namespace Dialect.SqlServer.Rendering;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Compilation;
using System.Text;

/// <summary>
/// SQL Server routine (stored procedure / function) renderer.
/// </summary>
public sealed class SqlServerRoutineRenderer : IRoutineRenderer
{
    public CompiledQuery Render(RoutineCall routine, ISqlDialect dialect)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var parameters = new Dictionary<string, object?>();
        var sb = new StringBuilder();

        // For SQL Server, stored procedures are called with EXEC
        sb.Append("EXEC ");

        if (!string.IsNullOrEmpty(routine.Schema))
            sb.Append($"[{routine.Schema}].");

        sb.Append($"[{routine.Name}]");

        var paramList = new List<string>();
        foreach (var param in routine.Parameters)
        {
            var paramName = $"@{param.Name}";
            if (param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.InputOutput)
                paramName += " OUTPUT";

            if (param.Value != null)
                parameters[param.Name] = param.Value;

            paramList.Add(paramName);
        }

        if (paramList.Count > 0)
            sb.Append(" ").Append(string.Join(", ", paramList));

        return new CompiledQuery(sb.ToString(), parameters);
    }
}
