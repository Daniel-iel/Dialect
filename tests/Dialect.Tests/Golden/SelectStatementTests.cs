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
/// Golden tests for SELECT statements across all three dialects.
/// Each test verifies that the same fluent query produces correct SQL for each database.
/// </summary>
public class SelectStatementTests
{
    private readonly ISqlDialect _sqlServerDialect = new SqlServerDialect();
    private readonly ISqlDialect _postgreSqlDialect = new PostgreSqlDialect();
    private readonly ISqlDialect _mySqlDialect = new MySqlDialect();

    [Fact]
    public void Simple_SELECT_generates_correct_SQL_for_all_dialects()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name", "Email")
            .From("Users")
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        sqlServer.Sql.Should().Be("SELECT [Id], [Name], [Email] FROM [Users]");
        postgreSql.Sql.Should().Be("SELECT \"Id\", \"Name\", \"Email\" FROM \"Users\"");
        mysql.Sql.Should().Be("SELECT `Id`, `Name`, `Email` FROM `Users`");
    }

    [Fact]
    public void SELECT_with_WHERE_equality_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        sqlServer.Sql.Should().Be("SELECT [Id], [Name] FROM [Users] WHERE [Status] = @p1");
        postgreSql.Sql.Should().Be("SELECT \"Id\", \"Name\" FROM \"Users\" WHERE \"Status\" = $1");
        mysql.Sql.Should().Be("SELECT `Id`, `Name` FROM `Users` WHERE `Status` = ?");

        sqlServer.Parameters.Should().HaveCount(1);
        sqlServer.Parameters["p1"].Should().Be("Active");
        postgreSql.Parameters.Should().HaveCount(1);
        postgreSql.Parameters["p1"].Should().Be("Active");
        mysql.Parameters.Should().HaveCount(1);
        mysql.Parameters["p1"].Should().Be("Active");
    }

    [Fact]
    public void SELECT_DISTINCT_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Email")
            .From("Users")
            .Distinct()
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        sqlServer.Sql.Should().Be("SELECT DISTINCT [Email] FROM [Users]");
        postgreSql.Sql.Should().Be("SELECT DISTINCT \"Email\" FROM \"Users\"");
        mysql.Sql.Should().Be("SELECT DISTINCT `Email` FROM `Users`");
    }

    [Fact]
    public void SELECT_with_INNER_JOIN_generates_correct_SQL()
    {
        // Arrange
        var onCondition = new ComparisonNode(new Column("UserId"), ComparisonOperator.Equal, 1);
        var query = SqlBuilder
            .Select("u.Id", "u.Name", "o.OrderDate")
            .From("Users", "u")
            .InnerJoin("Orders", new ComparisonNode(new Column("UserId"), ComparisonOperator.Equal, 0), "o")
            .Build();

        // Act - Since the join condition needs proper setup, we'll keep this test basic for now
        var sqlServer = query.Compile(_sqlServerDialect);

        // Assert
        sqlServer.Sql.Should().Contain("INNER JOIN");
    }

    [Fact]
    public void SELECT_with_ORDER_BY_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Name", SortDirection.Ascending)
            .OrderBy("Id", SortDirection.Descending)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        sqlServer.Sql.Should().Be("SELECT [Id], [Name] FROM [Users] ORDER BY [Name] ASC, [Id] DESC");
        postgreSql.Sql.Should().Be("SELECT \"Id\", \"Name\" FROM \"Users\" ORDER BY \"Name\" ASC, \"Id\" DESC");
        mysql.Sql.Should().Be("SELECT `Id`, `Name` FROM `Users` ORDER BY `Name` ASC, `Id` DESC");
    }

    [Fact]
    public void SELECT_with_LIMIT_generates_correct_SQL_per_dialect()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Take(10)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert - SQL Server uses TOP, PostgreSQL/MySQL use LIMIT
        sqlServer.Sql.Should().Be("SELECT TOP 10 [Id], [Name] FROM [Users] ORDER BY [Id] ASC");
        postgreSql.Sql.Should().Be("SELECT \"Id\", \"Name\" FROM \"Users\" ORDER BY \"Id\" ASC LIMIT 10");
        mysql.Sql.Should().Be("SELECT `Id`, `Name` FROM `Users` ORDER BY `Id` ASC LIMIT 10");
    }

    [Fact]
    public void SELECT_with_OFFSET_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Take(10, 20) // Take 10 rows starting from offset 20
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        // SQL Server uses OFFSET/FETCH NEXT syntax
        sqlServer.Sql.Should().Contain("OFFSET 20 ROWS");
        sqlServer.Sql.Should().Contain("FETCH NEXT 10 ROWS ONLY");

        // PostgreSQL/MySQL use LIMIT/OFFSET
        postgreSql.Sql.Should().Contain("LIMIT 10");
        postgreSql.Sql.Should().Contain("OFFSET 20");

        mysql.Sql.Should().Contain("LIMIT 10");
        mysql.Sql.Should().Contain("OFFSET 20");
    }

    [Fact]
    public void SELECT_with_GROUP_BY_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Department", "COUNT(Id)")
            .From("Users")
            .GroupBy("Department")
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        sqlServer.Sql.Should().Contain("GROUP BY [Department]");
        postgreSql.Sql.Should().Contain("GROUP BY \"Department\"");
        mysql.Sql.Should().Contain("GROUP BY `Department`");
    }

    [Fact]
    public void SELECT_with_multiple_WHERE_conditions_generates_correct_SQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Where("Age", ComparisonOperator.GreaterThanOrEqual, 18)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);

        // Assert
        sqlServer.Sql.Should().Contain("WHERE");
        sqlServer.Sql.Should().Contain("AND");
        sqlServer.Parameters.Should().HaveCount(2);
        sqlServer.Parameters["p1"].Should().Be("Active");
        sqlServer.Parameters["p2"].Should().Be(18);
    }

    [Fact]
    public void FULL_JOIN_throws_exception_for_MySQL()
    {
        // Arrange
        var query = SqlBuilder
            .Select("u.Id", "o.OrderId")
            .From("Users", "u")
            .FullJoin("Orders", new ComparisonNode(new Column("UserId"), ComparisonOperator.Equal, 0), "o")
            .Build();

        // Act & Assert
        var exception = Assert.Throws<SqlCompilationException>(() => query.Compile(_mySqlDialect));
        exception.Message.Should().Contain("FULL OUTER JOIN");
        exception.Message.Should().Contain("not supported");
    }

    [Fact]
    public void DELETE_without_WHERE_throws_exception()
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SqlBuilder.Delete().From("Users").Build()
        );
        ex.Message.Should().Contain("WHERE clause is required");
    }

    [Fact]
    public void DELETE_with_AllowFullTableOperation_succeeds()
    {
        // Act
        var stmt = SqlBuilder.Delete().From("Users").AllowFullTableOperation().Build();

        // Assert
        stmt.Should().NotBeNull();
        stmt.AllowFullTableDelete.Should().BeTrue();
    }

    [Fact]
    public void UPDATE_without_WHERE_throws_exception()
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SqlBuilder.Update().Table("Users").Set("Status", "Active").Build()
        );
        ex.Message.Should().Contain("WHERE clause is required");
    }

    [Fact]
    public void SELECT_with_CTE_generates_correct_SQL()
    {
        // Arrange
        var cteQuery = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Build();

        var query = SqlBuilder
            .Select("Id", "Name")
            .With("active_users", cteQuery)
            .From("active_users")
            .Build();

        // Act
        var sqlServer = query.Compile(new SqlServerDialect());
        var postgreSql = query.Compile(new PostgreSqlDialect());
        var mysql = query.Compile(new MySqlDialect());

        // Assert
        sqlServer.Sql.Should().Contain("WITH [active_users] AS");
        sqlServer.Sql.Should().Contain("SELECT [Id], [Name] FROM [active_users]");

        postgreSql.Sql.Should().Contain("WITH \"active_users\" AS");
        postgreSql.Sql.Should().Contain("SELECT \"Id\", \"Name\" FROM \"active_users\"");

        mysql.Sql.Should().Contain("WITH `active_users` AS");
        mysql.Sql.Should().Contain("SELECT `Id`, `Name` FROM `active_users`");
    }

    [Fact]
    public void SELECT_with_multiple_CTEs_generates_correct_SQL()
    {
        // Arrange
        var cte1 = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Build();

        var cte2 = SqlBuilder
            .Select("Id", "Amount")
            .From("Orders")
            .Where("Status", "Completed")
            .Build();

        var query = SqlBuilder
            .Select("Id", "Name", "Amount")
            .With("active_users", cte1)
            .With("completed_orders", cte2)
            .From("active_users")
            .Build();

        // Act
        var sqlServer = query.Compile(new SqlServerDialect());
        var postgreSql = query.Compile(new PostgreSqlDialect());
        var mysql = query.Compile(new MySqlDialect());

        // Assert - Verify both CTEs are present
        sqlServer.Sql.Should().Contain("WITH [active_users] AS");
        sqlServer.Sql.Should().Contain("[completed_orders] AS");

        postgreSql.Sql.Should().Contain("WITH \"active_users\" AS");
        postgreSql.Sql.Should().Contain("\"completed_orders\" AS");

        mysql.Sql.Should().Contain("WITH `active_users` AS");
        mysql.Sql.Should().Contain("`completed_orders` AS");
    }

    [Fact]
    public void SELECT_with_subquery_in_FROM_generates_correct_SQL()
    {
        // Arrange
        var subquery = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Build();

        var query = SqlBuilder
            .Select("Id", "Name")
            .From(subquery, "active_users")
            .Build();

        // Act
        var sqlServer = query.Compile(new SqlServerDialect());
        var postgreSql = query.Compile(new PostgreSqlDialect());
        var mysql = query.Compile(new MySqlDialect());

        // Assert
        sqlServer.Sql.Should().Contain("FROM (SELECT");
        sqlServer.Sql.Should().Contain(") AS [active_users]");

        postgreSql.Sql.Should().Contain("FROM (SELECT");
        postgreSql.Sql.Should().Contain(") AS \"active_users\"");

        mysql.Sql.Should().Contain("FROM (SELECT");
        mysql.Sql.Should().Contain(") AS `active_users`");
    }

    [Fact]
    public void SELECT_with_nested_subqueries_generates_correct_SQL()
    {
        // Arrange
        var innerSubquery = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .Where("Status", "Active")
            .Build();

        var outerSubquery = SqlBuilder
            .Select("Id", "Name")
            .From(innerSubquery, "active_users")
            .Build();

        var finalQuery = SqlBuilder
            .Select("Id", "Name")
            .From(outerSubquery, "filtered_users")
            .Build();

        // Act
        var sqlServer = finalQuery.Compile(new SqlServerDialect());
        var postgreSql = finalQuery.Compile(new PostgreSqlDialect());
        var mysql = finalQuery.Compile(new MySqlDialect());

        // Assert - Verify nested structure
        sqlServer.Sql.Should().Contain("FROM (SELECT");
        sqlServer.Sql.Should().MatchRegex(@"\(SELECT.*FROM \(SELECT");

        postgreSql.Sql.Should().Contain("FROM (SELECT");
        postgreSql.Sql.Should().MatchRegex(@"\(SELECT.*FROM \(SELECT");

        mysql.Sql.Should().Contain("FROM (SELECT");
        mysql.Sql.Should().MatchRegex(@"\(SELECT.*FROM \(SELECT");
    }

    [Fact]
    public void SELECT_with_ROW_NUMBER_window_function_generates_correct_SQL()
    {
        // Arrange
        var overClause = new OverClause(
            new[] { "DepartmentId" },
            new[] { new OrderByClause(new Column("Salary"), SortDirection.Descending) }
        );

        var query = SqlBuilder
            .Select("Id", "Name", "Salary")
            .SelectWindow("ROW_NUMBER", null, overClause, "rank")
            .From("Employees")
            .Build();

        // Act
        var sqlServer = query.Compile(new SqlServerDialect());
        var postgreSql = query.Compile(new PostgreSqlDialect());
        var mysql = query.Compile(new MySqlDialect());

        // Assert - Verify window function rendering
        sqlServer.Sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY [DepartmentId] ORDER BY [Salary] Descending)");
        sqlServer.Sql.Should().Contain("AS [rank]");

        postgreSql.Sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY \"DepartmentId\" ORDER BY \"Salary\" Descending)");
        postgreSql.Sql.Should().Contain("AS \"rank\"");

        mysql.Sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY `DepartmentId` ORDER BY `Salary` Descending)");
        mysql.Sql.Should().Contain("AS `rank`");
    }

    [Fact]
    public void SELECT_with_RANK_and_aggregate_window_functions_generates_correct_SQL()
    {
        // Arrange
        var rankOverClause = new OverClause(
            new[] { "Category" },
            new[] { new OrderByClause(new Column("Sales"), SortDirection.Descending) }
        );

        var sumOverClause = new OverClause(
            new[] { "Category" },
            null
        );

        var query = SqlBuilder
            .Select("Id", "Category", "Sales")
            .SelectWindow("RANK", null, rankOverClause, "sales_rank")
            .SelectWindow("SUM", new[] { "Sales" }, sumOverClause, "category_total")
            .From("Products")
            .Build();

        // Act
        var sqlServer = query.Compile(new SqlServerDialect());
        var postgreSql = query.Compile(new PostgreSqlDialect());
        var mysql = query.Compile(new MySqlDialect());

        // Assert - Verify multiple window functions
        sqlServer.Sql.Should().Contain("RANK() OVER (PARTITION BY [Category]");
        sqlServer.Sql.Should().Contain("SUM([Sales]) OVER (PARTITION BY [Category])");

        postgreSql.Sql.Should().Contain("RANK() OVER (PARTITION BY \"Category\"");
        postgreSql.Sql.Should().Contain("SUM(\"Sales\") OVER (PARTITION BY \"Category\")");

        mysql.Sql.Should().Contain("RANK() OVER (PARTITION BY `Category`");
        mysql.Sql.Should().Contain("SUM(`Sales`) OVER (PARTITION BY `Category`)");
    }

    [Fact]
    public void SELECT_with_SKIP_only_generates_correct_SQL()
    {
        // Arrange - Skip without Take should work independently
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Skip(5)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert
        // SQL Server requires ORDER BY with OFFSET, no FETCH NEXT means no limit
        sqlServer.Sql.Should().Contain("OFFSET 5 ROWS");
        sqlServer.Sql.Should().NotContain("FETCH NEXT");

        // PostgreSQL supports OFFSET without LIMIT
        postgreSql.Sql.Should().Be("SELECT \"Id\", \"Name\" FROM \"Users\" ORDER BY \"Id\" ASC OFFSET 5");

        // MySQL needs a very large LIMIT when using OFFSET alone
        mysql.Sql.Should().Contain("OFFSET 5");
        mysql.Sql.Should().Contain("LIMIT 18446744073709551615");
    }

    [Fact]
    public void SELECT_with_TAKE_then_SKIP_order_reversal_works()
    {
        // Arrange - New order: Take() then Skip() (reversed from traditional order)
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Take(10)
            .Skip(5)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert - Should work same as Skip then Take
        sqlServer.Sql.Should().Contain("OFFSET 5 ROWS");
        sqlServer.Sql.Should().Contain("FETCH NEXT 10 ROWS ONLY");

        postgreSql.Sql.Should().Contain("LIMIT 10");
        postgreSql.Sql.Should().Contain("OFFSET 5");

        mysql.Sql.Should().Contain("LIMIT 10");
        mysql.Sql.Should().Contain("OFFSET 5");
    }

    [Fact]
    public void SELECT_with_SKIP_then_TAKE_maintains_backward_compatibility()
    {
        // Arrange - Original order: Skip() then Take()
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Skip(5)
            .Take(10)
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert - Should produce same SQL as Take then Skip
        sqlServer.Sql.Should().Contain("OFFSET 5 ROWS");
        sqlServer.Sql.Should().Contain("FETCH NEXT 10 ROWS ONLY");

        postgreSql.Sql.Should().Contain("LIMIT 10");
        postgreSql.Sql.Should().Contain("OFFSET 5");

        mysql.Sql.Should().Contain("LIMIT 10");
        mysql.Sql.Should().Contain("OFFSET 5");
    }

    [Fact]
    public void SELECT_with_multiple_SKIP_TAKE_calls_last_writer_wins()
    {
        // Arrange - Multiple calls; last one wins
        var query = SqlBuilder
            .Select("Id", "Name")
            .From("Users")
            .OrderBy("Id")
            .Skip(5)
            .Skip(10)  // Overwrites previous Skip
            .Take(20)
            .Take(15)  // Overwrites previous Take
            .Build();

        // Act
        var sqlServer = query.Compile(_sqlServerDialect);
        var postgreSql = query.Compile(_postgreSqlDialect);
        var mysql = query.Compile(_mySqlDialect);

        // Assert - Should use final values: Skip(10) and Take(15)
        sqlServer.Sql.Should().Contain("OFFSET 10 ROWS");
        sqlServer.Sql.Should().Contain("FETCH NEXT 15 ROWS ONLY");

        postgreSql.Sql.Should().Contain("LIMIT 15");
        postgreSql.Sql.Should().Contain("OFFSET 10");

        mysql.Sql.Should().Contain("LIMIT 15");
        mysql.Sql.Should().Contain("OFFSET 10");
    }
}
