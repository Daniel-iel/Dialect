namespace Dialect.Tests.Golden;

using Dialect.Core.AST;
using Dialect.Core.Fluent;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.SqlServer;
using Dialect.PostgreSql;
using Dialect.MySql;
using Xunit;
using FluentAssertions;

/// <summary>
/// Golden tests for UPSERT statements across all three dialects.
/// Each test verifies that the same fluent query produces correct SQL for each database.
/// </summary>
public class UpsertStatementTests
{
    private readonly ISqlDialect _sqlServerDialect = new SqlServerDialect();
    private readonly ISqlDialect _postgreSqlDialect = new PostgreSqlDialect();
    private readonly ISqlDialect _mySqlDialect = new MySqlDialect();

    [Fact]
    public void UPSERT_single_row_single_conflict_column_generates_correct_SQL()
    {
        // Arrange: INSERT OR UPDATE on a user by ID
        var upsert = SqlBuilder
            .Upsert("Users")
            .Columns("Id", "Name", "Email")
            .Values(1, "John Doe", "john@example.com")
            .OnConflict("Id")
            .UpdateSet("Name", "Jane Doe")
            .UpdateSet("Email", "jane@example.com")
            .Build();

        // Act
        var sqlServer = upsert.Compile(_sqlServerDialect);
        var postgreSql = upsert.Compile(_postgreSqlDialect);
        var mysql = upsert.Compile(_mySqlDialect);

        // Assert - SQL Server MERGE syntax
        sqlServer.Sql.Should().Contain("MERGE INTO [Users] AS target");
        sqlServer.Sql.Should().Contain("ON target.[Id] = source.[Id]");
        sqlServer.Sql.Should().Contain("WHEN MATCHED THEN UPDATE SET");
        sqlServer.Sql.Should().Contain("[Name]");
        sqlServer.Sql.Should().Contain("[Email]");
        sqlServer.Sql.Should().Contain("WHEN NOT MATCHED BY TARGET THEN INSERT");
        sqlServer.Parameters.Should().HaveCount(5); // 3 INSERT values + 2 UPDATE values

        // Assert - PostgreSQL ON CONFLICT syntax
        postgreSql.Sql.Should().Contain("INSERT INTO \"Users\"");
        postgreSql.Sql.Should().Contain("ON CONFLICT (\"Id\") DO UPDATE SET");
        postgreSql.Sql.Should().Contain("EXCLUDED");
        postgreSql.Parameters.Should().HaveCount(3); // Only INSERT values

        // Assert - MySQL ON DUPLICATE KEY UPDATE syntax
        mysql.Sql.Should().Contain("INSERT INTO `Users`");
        mysql.Sql.Should().Contain("ON DUPLICATE KEY UPDATE");
        mysql.Sql.Should().Contain("VALUES(`Name`)");
        mysql.Sql.Should().Contain("VALUES(`Email`)");
        mysql.Parameters.Should().HaveCount(3); // Only INSERT values
    }

    [Fact]
    public void UPSERT_multiple_conflict_columns_generates_correct_SQL()
    {
        // Arrange: INSERT OR UPDATE on product by (SupplierId, PartNumber)
        var upsert = SqlBuilder
            .Upsert("Products")
            .Columns("SupplierId", "PartNumber", "Quantity", "Price")
            .Values(100, "PART-001", 50, 29.99)
            .OnConflict("SupplierId", "PartNumber")
            .UpdateSet("Quantity", 75)
            .UpdateSet("Price", 34.99)
            .Build();

        // Act
        var sqlServer = upsert.Compile(_sqlServerDialect);
        var postgreSql = upsert.Compile(_postgreSqlDialect);
        var mysql = upsert.Compile(_mySqlDialect);

        // Assert - SQL Server with multiple conflict columns
        sqlServer.Sql.Should().Contain("ON target.[SupplierId] = source.[SupplierId] AND target.[PartNumber] = source.[PartNumber]");
        sqlServer.Sql.Should().Contain("WHEN MATCHED THEN UPDATE SET");
        sqlServer.Parameters.Should().HaveCount(6); // 4 INSERT values + 2 UPDATE values

        // Assert - PostgreSQL with multiple conflict columns
        postgreSql.Sql.Should().Contain("ON CONFLICT (\"SupplierId\", \"PartNumber\") DO UPDATE SET");
        postgreSql.Parameters.Should().HaveCount(4); // Only INSERT values

        // Assert - MySQL with multiple conflict columns (treated as single composite key)
        mysql.Sql.Should().Contain("INSERT INTO `Products`");
        mysql.Sql.Should().Contain("ON DUPLICATE KEY UPDATE");
        mysql.Parameters.Should().HaveCount(4); // Only INSERT values
    }

    [Fact]
    public void UPSERT_three_column_update_generates_correct_SQL()
    {
        // Arrange: More complex UPSERT with 3 columns updated on conflict
        var upsert = SqlBuilder
            .Upsert("Inventory", schema: null)
            .Columns("ProductId", "Warehouse", "Stock", "LastUpdated", "Status")
            .Values(42, "WH-A", 100, "2024-01-15", "Active")
            .OnConflict("ProductId", "Warehouse")
            .UpdateSet("Stock", 150)
            .UpdateSet("LastUpdated", "2024-01-16")
            .UpdateSet("Status", "Modified")
            .Build();

        // Act
        var sqlServer = upsert.Compile(_sqlServerDialect);
        var postgreSql = upsert.Compile(_postgreSqlDialect);
        var mysql = upsert.Compile(_mySqlDialect);

        // Assert - All dialects should produce valid SQL without exceptions
        sqlServer.Sql.Should().NotBeNullOrEmpty();
        postgreSql.Sql.Should().NotBeNullOrEmpty();
        mysql.Sql.Should().NotBeNullOrEmpty();

        // SQL Server should have the right structure
        sqlServer.Sql.Should().Contain("MERGE INTO [Inventory]");
        sqlServer.Sql.Should().Contain("WHEN MATCHED");
        sqlServer.Sql.Should().Contain("WHEN NOT MATCHED BY TARGET");
        sqlServer.Parameters.Should().HaveCount(8); // 5 INSERT + 3 UPDATE

        // PostgreSQL should have ON CONFLICT
        postgreSql.Sql.Should().Contain("ON CONFLICT");
        postgreSql.Sql.Should().Contain("EXCLUDED");
        postgreSql.Parameters.Should().HaveCount(5); // Only INSERT values

        // MySQL should have ON DUPLICATE KEY UPDATE
        mysql.Sql.Should().Contain("ON DUPLICATE KEY UPDATE");
        mysql.Parameters.Should().HaveCount(5); // Only INSERT values
    }

    [Fact]
    public void UPSERT_builder_validates_column_names()
    {
        // Arrange & Act & Assert
        var ex = Record.Exception(() =>
            SqlBuilder
                .Upsert("Users")
                .Columns("") // Invalid: empty column name
                .Build()
        );

        ex.Should().NotBeNull();
        ex.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public void UPSERT_builder_requires_at_least_one_column()
    {
        // Arrange & Act & Assert
        var ex = Record.Exception(() =>
            SqlBuilder
                .Upsert("Users")
                .Build() // No columns added
        );

        ex.Should().NotBeNull();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void UPSERT_builder_validates_value_count_matches_column_count()
    {
        // Arrange & Act & Assert
        var ex = Record.Exception(() =>
            SqlBuilder
                .Upsert("Users")
                .Columns("Id", "Name")
                .Values(1) // Only 1 value for 2 columns
                .Build()
        );

        ex.Should().NotBeNull();
        ex.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public void UPSERT_builder_requires_conflict_columns()
    {
        // Arrange & Act & Assert
        // When OnConflict is not called, conflictClause will have null ConflictColumns
        // This should fail during Build() when the UpsertStatement validates
        var ex = Record.Exception(() =>
            SqlBuilder
                .Upsert("Users")
                .Columns("Id", "Name")
                .Values(1, "John")
                .Build() // No OnConflict() called
        );

        ex.Should().NotBeNull();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void UPSERT_builder_requires_at_least_one_update_clause()
    {
        // Arrange & Act & Assert
        var ex = Record.Exception(() =>
            SqlBuilder
                .Upsert("Users")
                .Columns("Id", "Name")
                .Values(1, "John")
                .OnConflict("Id")
                .Build() // No UpdateSet() called
        );

        ex.Should().NotBeNull();
        ex.Should().BeOfType<InvalidOperationException>();
    }
}
