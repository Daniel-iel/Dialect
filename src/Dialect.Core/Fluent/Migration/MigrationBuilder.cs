namespace Dialect.Core.Fluent.Migration;

using Dialect.Core.AST.Migration;

/// <summary>
/// Fluent builder for creating database migrations.
/// </summary>
public sealed class MigrationBuilder
{
    private readonly string _name;
    private readonly string _version;
    private readonly DateTime _timestamp;
    private readonly List<MigrationStep> _steps = new();

    /// <summary>
    /// Creates a new migration builder.
    /// </summary>
    /// <param name="name">Migration name (e.g., "CreateUsersTable")</param>
    /// <param name="version">Migration version (e.g., "001", "20240115_001")</param>
    /// <param name="timestamp">Migration timestamp (defaults to now)</param>
    public MigrationBuilder(string name, string version, DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Migration name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Migration version cannot be empty", nameof(version));

        _name = name;
        _version = version;
        _timestamp = timestamp ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Starts defining a new table.
    /// </summary>
    public TableBuilder CreateTable(string tableName)
    {
        var tableBuilder = new TableBuilder(tableName);
        return tableBuilder;
    }

    /// <summary>
    /// Completes a table definition and adds it to the migration.
    /// </summary>
    public MigrationBuilder WithTable(TableBuilder tableBuilder)
    {
        if (tableBuilder == null)
            throw new ArgumentNullException(nameof(tableBuilder));

        var step = tableBuilder.Build();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Drops a table from the database.
    /// </summary>
    public MigrationBuilder DropTable(string tableName, bool cascade = false)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var step = new DropTableStep(tableName, cascade);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a column to an existing table.
    /// </summary>
    public AddColumnContext AddColumn(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var columnBuilder = new ColumnBuilder(columnName);
        return new AddColumnContext(this, tableName, columnBuilder);
    }

    /// <summary>
    /// Completes adding a column to the migration.
    /// </summary>
    internal MigrationBuilder CompleteAddColumn(string tableName, ColumnDef column)
    {
        var step = new AddColumnStep(tableName, column);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Drops a column from a table.
    /// </summary>
    public MigrationBuilder DropColumn(string tableName, string columnName, bool cascade = false)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));

        var step = new DropColumnStep(tableName, columnName, cascade);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Creates an index on one or more columns.
    /// </summary>
    public MigrationBuilder AddIndex(string tableName, params string[] columnNames)
    {
        return AddIndex(tableName, isUnique: false, indexName: null, columnNames: columnNames);
    }

    /// <summary>
    /// Creates a unique index.
    /// </summary>
    public MigrationBuilder AddUniqueIndex(string tableName, params string[] columnNames)
    {
        return AddIndex(tableName, isUnique: true, indexName: null, columnNames: columnNames);
    }

    /// <summary>
    /// Creates an index with custom name.
    /// </summary>
    public MigrationBuilder AddIndex(string tableName, string indexName, params string[] columnNames)
    {
        return AddIndex(tableName, isUnique: false, indexName: indexName, columnNames: columnNames);
    }

    private MigrationBuilder AddIndex(string tableName, bool isUnique, string? indexName, params string[] columnNames)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("At least one column must be specified", nameof(columnNames));

        var step = new AddIndexStep(tableName, columnNames, indexName, isUnique);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Drops an index from a table.
    /// </summary>
    public MigrationBuilder DropIndex(string tableName, string indexName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException("Index name cannot be empty", nameof(indexName));

        var step = new DropIndexStep(tableName, indexName);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a foreign key constraint.
    /// </summary>
    public MigrationBuilder AddForeignKey(string tableName, string columnName, string referencedTable, string referencedColumn)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var step = new AddForeignKeyStep(tableName, columnName, referencedTable, referencedColumn);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Drops a foreign key constraint.
    /// </summary>
    public MigrationBuilder DropForeignKey(string tableName, string constraintName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var step = new DropForeignKeyStep(tableName, constraintName);
        step.Validate();
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Builds the immutable Migration.
    /// </summary>
    public Dialect.Core.AST.Migration.Migration Build()
    {
        var migration = new Dialect.Core.AST.Migration.Migration(
            Name: _name,
            Version: _version,
            Timestamp: _timestamp,
            Steps: _steps.AsReadOnly()
        );

        migration.Validate();
        return migration;
    }
}

/// <summary>
/// Helper for AddColumn fluent interface.
/// </summary>
public sealed class AddColumnContext
{
    private readonly MigrationBuilder _migrationBuilder;
    private readonly string _tableName;
    private readonly ColumnBuilder _columnBuilder;

    internal AddColumnContext(MigrationBuilder migrationBuilder, string tableName, ColumnBuilder columnBuilder)
    {
        _migrationBuilder = migrationBuilder;
        _tableName = tableName;
        _columnBuilder = columnBuilder;
    }

    /// <summary>
    /// Completes the column definition and returns to migration builder.
    /// </summary>
    public MigrationBuilder Done()
    {
        var column = _columnBuilder.Build();
        return _migrationBuilder.CompleteAddColumn(_tableName, column);
    }
}
