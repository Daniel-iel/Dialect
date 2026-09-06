namespace Dialect.Core.Fluent;

using Dialect.Core.AST;

/// <summary>
/// Fluent builder for routine calls (stored procedures and functions).
/// </summary>
public sealed class RoutineCallBuilder
{
    private readonly string _name;
    private readonly RoutineKind _kind;
    private string? _schema;
    private string? _package; // Reserved for future dialects like Oracle
    private readonly List<RoutineParameter> _parameters = new();

    internal RoutineCallBuilder(string name, RoutineKind kind)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _kind = kind;
    }

    /// <summary>
    /// Sets the schema for the routine.
    /// </summary>
    public RoutineCallBuilder InSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema cannot be empty", nameof(schema));

        _schema = schema;
        return this;
    }

    /// <summary>
    /// Sets the package (reserved for future use with dialects like Oracle).
    /// </summary>
    public RoutineCallBuilder InPackage(string package)
    {
        if (string.IsNullOrWhiteSpace(package))
            throw new ArgumentException("Package cannot be empty", nameof(package));

        _package = package;
        return this;
    }

    /// <summary>
    /// Adds an input parameter.
    /// </summary>
    public RoutineCallBuilder AddParameter(string name, object? value, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name cannot be empty", nameof(name));

        _parameters.Add(new RoutineParameter(name, value, ParameterDirection.Input, type));
        return this;
    }

    /// <summary>
    /// Adds an output parameter.
    /// </summary>
    public RoutineCallBuilder AddOutputParameter(string name, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name cannot be empty", nameof(name));

        _parameters.Add(new RoutineParameter(name, null, ParameterDirection.Output, type));
        return this;
    }

    /// <summary>
    /// Adds an input/output parameter.
    /// </summary>
    public RoutineCallBuilder AddInputOutputParameter(string name, object? value, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name cannot be empty", nameof(name));

        _parameters.Add(new RoutineParameter(name, value, ParameterDirection.InputOutput, type));
        return this;
    }

    /// <summary>
    /// Builds the immutable RoutineCall.
    /// </summary>
    public RoutineCall Build()
    {
        return new RoutineCall(_name, _kind, _parameters, _schema, _package);
    }
}
