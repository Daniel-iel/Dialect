namespace Dialect.Core.Fluent.Migration;

using Dialect.Core.AST.Migration;

/// <summary>
/// Fluent builder for defining a column in a migration.
/// </summary>
public sealed class ColumnBuilder
{
    private readonly string _name;
    private DataType _type;
    private bool _nullable = true;
    private object? _defaultValue;
    private bool _isAutoIncrement;
    private bool _isPrimaryKey;
    private int? _length;
    private (int, int)? _precision;

    internal ColumnBuilder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty", nameof(name));
        
        _name = name;
    }

    // Data type builders
    public ColumnBuilder AsInt()
    {
        _type = DataType.Int;
        return this;
    }

    public ColumnBuilder AsBigInt()
    {
        _type = DataType.BigInt;
        return this;
    }

    public ColumnBuilder AsString(int length)
    {
        _type = DataType.Varchar;
        _length = length;
        return this;
    }

    public ColumnBuilder AsText()
    {
        _type = DataType.Text;
        return this;
    }

    public ColumnBuilder AsDecimal(int precision, int scale)
    {
        _type = DataType.Decimal;
        _precision = (precision, scale);
        return this;
    }

    public ColumnBuilder AsDateTime()
    {
        _type = DataType.DateTime;
        return this;
    }

    public ColumnBuilder AsBoolean()
    {
        _type = DataType.Boolean;
        return this;
    }

    public ColumnBuilder AsUuid()
    {
        _type = DataType.Uuid;
        return this;
    }

    public ColumnBuilder AsJson()
    {
        _type = DataType.Json;
        return this;
    }

    // Constraints
    public ColumnBuilder NotNull()
    {
        _nullable = false;
        return this;
    }

    public ColumnBuilder Nullable()
    {
        _nullable = true;
        return this;
    }

    public ColumnBuilder WithDefault(object? value)
    {
        _defaultValue = value;
        return this;
    }

    public ColumnBuilder WithIdentity()
    {
        _isAutoIncrement = true;
        _nullable = false;
        return this;
    }

    public ColumnBuilder AsPrimaryKey()
    {
        _isPrimaryKey = true;
        _nullable = false;
        return this;
    }

    /// <summary>
    /// Builds the immutable ColumnDef.
    /// </summary>
    internal ColumnDef Build()
    {
        var column = new ColumnDef(
            Name: _name,
            Type: _type,
            Nullable: _nullable,
            DefaultValue: _defaultValue,
            IsAutoIncrement: _isAutoIncrement,
            IsPrimaryKey: _isPrimaryKey,
            Length: _length,
            Precision: _precision
        );

        column.Validate();
        return column;
    }
}
