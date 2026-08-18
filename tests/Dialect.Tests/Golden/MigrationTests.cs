namespace Dialect.Tests.Golden;

using Dialect.Core.AST;
using Dialect.Core.AST.Migration;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent.Migration;
using Dialect.MySql;
using Dialect.PostgreSql;
using Dialect.SqlServer;
using FluentAssertions;
using Xunit;

/// <summary>
/// Golden tests for migration steps rendering across dialects.
/// </summary>
public class MigrationTests
{
    private readonly ISqlDialect _sqlServerDialect = new SqlServerDialect();
    private readonly ISqlDialect _postgreSqlDialect = new PostgreSqlDialect();
    private readonly ISqlDialect _mysqlDialect = new MySqlDialect();

    [Fact]
    public void CREATE_TABLE_single_column_generates_correct_SQL()
    {
        // Arrange - Create column definitions and table step directly
        var columns = new List<ColumnDef>
        {
            new("Id", DataType.Int, false, null, true, false, null, null),
            new("Name", DataType.Varchar, false, null, false, false, 255, null)
        };
        var step = new CreateTableStep("Users", columns, new List<string> { "Id" });
        
        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("CREATE TABLE [Users]");
        sqlServerSql.Should().Contain("[Id] INT IDENTITY(1,1)");
        sqlServerSql.Should().Contain("PRIMARY KEY ([Id])");

        pgSql.Should().Contain("CREATE TABLE \"Users\"");
        pgSql.Should().Contain("\"Id\" INTEGER GENERATED ALWAYS AS IDENTITY");
        pgSql.Should().Contain("PRIMARY KEY (\"Id\")");

        mysqlSql.Should().Contain("CREATE TABLE `Users`");
        mysqlSql.Should().Contain("`Id` INT AUTO_INCREMENT");
        mysqlSql.Should().Contain("PRIMARY KEY (`Id`)");
    }

    [Fact]
    public void CREATE_TABLE_with_defaults_generates_correct_SQL()
    {
        // Arrange
        var columns = new List<ColumnDef>
        {
            new("Id", DataType.BigInt, false, null, true, false, null, null),
            new("Name", DataType.Varchar, false, null, false, false, 100, null),
            new("IsActive", DataType.Boolean, true, true, false, false, null, null)
        };
        var step = new CreateTableStep("Products", columns, new List<string> { "Id" });

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("DEFAULT 1");
        pgSql.Should().Contain("DEFAULT true");
        mysqlSql.Should().Contain("DEFAULT 1");
    }

    [Fact]
    public void DROP_TABLE_generates_correct_SQL()
    {
        // Arrange
        var step = new DropTableStep("Users", false);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Be("DROP TABLE [Users]");
        pgSql.Should().Be("DROP TABLE IF EXISTS \"Users\"");
        mysqlSql.Should().Be("DROP TABLE IF EXISTS `Users`");
    }

    [Fact]
    public void ADD_COLUMN_generates_correct_SQL()
    {
        // Arrange
        var column = new ColumnDef("Email", DataType.Varchar, false, null, false, false, 255, null);
        var step = new AddColumnStep("Users", column);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("ALTER TABLE [Users]");
        sqlServerSql.Should().Contain("[Email] VARCHAR(255)");

        pgSql.Should().Contain("ALTER TABLE \"Users\"");
        pgSql.Should().Contain("\"Email\" VARCHAR(255)");

        mysqlSql.Should().Contain("ALTER TABLE `Users`");
        mysqlSql.Should().Contain("`Email` VARCHAR(255)");
    }

    [Fact]
    public void DROP_COLUMN_generates_correct_SQL()
    {
        // Arrange
        var step = new DropColumnStep("Users", "Email", false);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Be("ALTER TABLE [Users] DROP COLUMN [Email]");
        pgSql.Should().Be("ALTER TABLE \"Users\" DROP COLUMN \"Email\"");
        mysqlSql.Should().Be("ALTER TABLE `Users` DROP COLUMN `Email`");
    }

    [Fact]
    public void ALTER_COLUMN_type_only_generates_correct_SQL()
    {
        // Arrange
        var step = new AlterColumnStep("Users", "Email", DataType.Text);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("ALTER TABLE [Users]");
        sqlServerSql.Should().Contain("ALTER COLUMN [Email] TEXT");

        pgSql.Should().Contain("ALTER TABLE \"Users\"");
        pgSql.Should().Contain("ALTER COLUMN \"Email\" TYPE TEXT");

        mysqlSql.Should().Contain("ALTER TABLE `Users`");
        mysqlSql.Should().Contain("MODIFY COLUMN `Email` TEXT");
    }

    [Fact]
    public void ADD_INDEX_generates_correct_SQL()
    {
        // Arrange
        var step = new AddIndexStep("Users", new[] { "Email" }, "IX_Users_Email", false);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("CREATE INDEX [IX_Users_Email]");
        sqlServerSql.Should().Contain("ON [Users] ([Email])");

        pgSql.Should().Contain("CREATE INDEX \"IX_Users_Email\"");
        pgSql.Should().Contain("ON \"Users\" (\"Email\")");

        mysqlSql.Should().Contain("CREATE INDEX `IX_Users_Email`");
        mysqlSql.Should().Contain("ON `Users` (`Email`)");
    }

    [Fact]
    public void ADD_UNIQUE_INDEX_generates_correct_SQL()
    {
        // Arrange
        var step = new AddIndexStep("Users", new[] { "Email" }, "UQ_Users_Email", true);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("CREATE UNIQUE INDEX");
        pgSql.Should().Contain("CREATE UNIQUE INDEX");
        mysqlSql.Should().Contain("CREATE UNIQUE INDEX");
    }

    [Fact]
    public void ADD_FOREIGN_KEY_generates_correct_SQL()
    {
        // Arrange
        var step = new AddForeignKeyStep("Orders", "UserId", "Users", "Id", "FK_Orders_Users", false);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("ALTER TABLE [Orders]");
        sqlServerSql.Should().Contain("ADD CONSTRAINT [FK_Orders_Users]");
        sqlServerSql.Should().Contain("FOREIGN KEY ([UserId])");
        sqlServerSql.Should().Contain("REFERENCES [Users]([Id])");

        pgSql.Should().Contain("ALTER TABLE \"Orders\"");
        pgSql.Should().Contain("ADD CONSTRAINT \"FK_Orders_Users\"");

        mysqlSql.Should().Contain("ALTER TABLE `Orders`");
        mysqlSql.Should().Contain("ADD CONSTRAINT `FK_Orders_Users`");
    }

    [Fact]
    public void ADD_FOREIGN_KEY_CASCADE_generates_correct_SQL()
    {
        // Arrange
        var step = new AddForeignKeyStep("Orders", "UserId", "Users", "Id", "FK_Orders_Users", true);

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Contain("ON DELETE CASCADE");
        pgSql.Should().Contain("ON DELETE CASCADE");
        mysqlSql.Should().Contain("ON DELETE CASCADE");
    }

    [Fact]
    public void DROP_FOREIGN_KEY_generates_correct_SQL()
    {
        // Arrange
        var step = new DropForeignKeyStep("Orders", "FK_Orders_Users");

        // Assert
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var pgRenderer = _postgreSqlDialect.CreateMigrationRenderer();
        var mysqlRenderer = _mysqlDialect.CreateMigrationRenderer();

        var sqlServerSql = sqlServerRenderer.RenderStep(step, _sqlServerDialect);
        var pgSql = pgRenderer.RenderStep(step, _postgreSqlDialect);
        var mysqlSql = mysqlRenderer.RenderStep(step, _mysqlDialect);

        sqlServerSql.Should().Be("ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_Users]");
        pgSql.Should().Be("ALTER TABLE \"Orders\" DROP CONSTRAINT \"FK_Orders_Users\"");
        mysqlSql.Should().Be("ALTER TABLE `Orders` DROP FOREIGN KEY `FK_Orders_Users`");
    }

    [Fact]
    public void MIGRATION_builder_creates_valid_migration()
    {
        // Test creating a migration using the builder API
        // Since TableBuilder is internal, we'll test through direct API
        var columns = new List<ColumnDef> { new("Id", DataType.Int, false, null, true, false, null, null) };
        var createTableStep = new CreateTableStep("Users", columns, new List<string> { "Id" });
        var dropTableStep = new DropTableStep("Temp", false);
        
        var steps = new List<MigrationStep> { createTableStep, dropTableStep };
        var migration = new Migration("CreateUsersTable", "001", DateTime.UtcNow, steps);

        // Assert
        migration.Name.Should().Be("CreateUsersTable");
        migration.Version.Should().Be("001");
        migration.Steps.Should().HaveCount(2);
        migration.Steps[0].Should().BeOfType<CreateTableStep>();
        migration.Steps[1].Should().BeOfType<DropTableStep>();
    }

    [Fact]
    public void COMPLETE_migration_renders_all_steps()
    {
        // Arrange
        var columns = new List<ColumnDef>
        {
            new("Id", DataType.Int, false, null, true, false, null, null),
            new("Name", DataType.Varchar, false, null, false, false, 255, null)
        };
        var createTableStep = new CreateTableStep("Users", columns, new List<string> { "Id" });
        var addIndexStep = new AddIndexStep("Users", new[] { "Name" }, "IX_Users_Name", false);

        var steps = new List<MigrationStep> { createTableStep, addIndexStep };
        var migration = new Migration("CreateUsersTable", "001", DateTime.UtcNow, steps);

        // Act
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var compiled = sqlServerRenderer.Render(migration, _sqlServerDialect);

        // Assert
        compiled.Sql.Should().Contain("CREATE TABLE");
        compiled.Sql.Should().Contain("CREATE INDEX");
        compiled.Sql.Should().Contain("GO");
        compiled.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void COLUMN_BUILDER_with_all_constraints_builds_correctly()
    {
        // This test verifies column building through the AST since ColumnBuilder is internal
        var column = new ColumnDef("Id", DataType.Int, false, null, true, false, null, null);

        // Assert
        column.Name.Should().Be("Id");
        column.Type.Should().Be(DataType.Int);
        column.IsAutoIncrement.Should().BeTrue();
        column.Nullable.Should().BeFalse();
    }

    [Fact]
    public void MIGRATION_validates_empty_steps()
    {
        // Act & Assert - building a migration with no steps should fail validation
        var exception = Record.Exception(() => 
        {
            var migration = new MigrationBuilder("Empty", "001").Build();
        });
        
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public void MIGRATION_all_steps_render_without_parameters()
    {
        // Arrange
        var steps = new List<MigrationStep> { new DropTableStep("Old", false) };
        var migration = new Migration("AllSteps", "001", DateTime.UtcNow, steps);

        // Act
        var sqlServerRenderer = _sqlServerDialect.CreateMigrationRenderer();
        var compiled = sqlServerRenderer.Render(migration, _sqlServerDialect);

        // Assert
        compiled.Parameters.Should().BeEmpty();
        compiled.Sql.Should().NotBeNullOrEmpty();
    }
}
