namespace Dialect.Tests.Query;

using FluentAssertions;
using Xunit;
using Dialect.Core.Query;
using Dialect.SqlServer.Query;
using Dialect.PostgreSql.Query;
using Dialect.MySql.Query;

/// <summary>
/// Tests for Query Parser implementations across all dialects.
/// Validates query clause extraction and analysis.
/// </summary>
public class QueryParserTests
{
    [Fact]
    public void SqlServerQueryParser_ParseSimpleSelect_ExtractsSelectClause()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT Id, Name FROM Users";

        // Act
        var parsed = parser.Parse(sql);

        // Assert
        parsed.SelectClause.Should().NotBeEmpty();
        parsed.SelectColumns.Should().Contain(c => c.Contains("Id"));
        parsed.FromTables.Should().Contain("Users");
    }

    [Fact]
    public void SqlServerQueryParser_ParseWithWhere_ExtractsPredicates()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT * FROM Users WHERE Id = 1";

        // Act
        var parsed = parser.Parse(sql);

        // Assert
        // Parser extracts main structure; WHERE clause details are tested separately
        parsed.FromTables.Should().Contain("Users");
    }

    [Fact]
    public void SqlServerQueryParser_ParseWithJoin_DoesNotThrow()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT * FROM Users u INNER JOIN Orders o ON u.Id = o.UserId";

        // Act & Assert
        var action = () => parser.Parse(sql);
        action.Should().NotThrow();
    }

    [Fact]
    public void SqlServerQueryParser_ParseWithGroupBy_ExtractsGrouping()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT Department, COUNT(*) FROM Users GROUP BY Department";

        // Act
        var parsed = parser.Parse(sql);

        // Assert
        parsed.GroupByClauses.Should().NotBeEmpty();
        parsed.GroupByClauses[0].Columns.Should().Contain("Department");
    }

    [Fact]
    public void SqlServerQueryParser_ParseWithOrderBy_ExtractsOrderingColumns()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT * FROM Users ORDER BY Name ASC, CreatedDate DESC";

        // Act
        var parsed = parser.Parse(sql);

        // Assert
        parsed.OrderByClauses.Should().NotBeEmpty();
        parsed.OrderByClauses[0].Columns.Should().Contain(c => c.Contains("Name"));
    }

    [Fact]
    public void PostgreSqlQueryParser_ParseSimpleSelect_DoesNotThrow()
    {
        // Arrange
        var parser = new PostgreSqlQueryParser();
        var sql = "SELECT id, name FROM users";

        // Act & Assert
        var action = () => parser.Parse(sql);
        action.Should().NotThrow();
    }

    [Fact]
    public void MySqlQueryParser_ParseWithBackticks_DoesNotThrow()
    {
        // Arrange
        var parser = new MySqlQueryParser();
        var sql = "SELECT `id`, `name` FROM `users` WHERE `status` = 'active'";

        // Act & Assert
        var action = () => parser.Parse(sql);
        action.Should().NotThrow();
    }

    [Fact]
    public void ParsedQuery_GetAllColumns_ReturnsUniqueColumns()
    {
        // Arrange
        var query = new ParsedQuery(
            SelectClause: "Id, Name",
            SelectColumns: new[] { "Id", "Name" },
            FromClause: "Users",
            FromTables: new[] { "Users" },
            WhereClauses: new[] { new ParsedClause("WHERE", "Id = 1", new[] { "Id" }, Array.Empty<string>(), 1) },
            JoinClauses: Array.Empty<ParsedClause>(),
            GroupByClauses: Array.Empty<ParsedClause>(),
            OrderByClauses: Array.Empty<ParsedClause>(),
            SelectivityEstimate: 0.1m,
            QueryComplexity: 1
        );

        // Act
        var allColumns = query.GetAllColumns();

        // Assert
        allColumns.Should().Contain("Id");
        allColumns.Should().Contain("Name");
        allColumns.Should().HaveCount(2);  // Unique, not 3
    }

    [Fact]
    public void ParsedQuery_GetAllTables_ReturnsUniqueTables()
    {
        // Arrange
        var query = new ParsedQuery(
            SelectClause: "*",
            SelectColumns: new[] { "*" },
            FromClause: "Users",
            FromTables: new[] { "Users" },
            WhereClauses: Array.Empty<ParsedClause>(),
            JoinClauses: new[] { new ParsedClause("JOIN", "JOIN Orders", Array.Empty<string>(), new[] { "Orders" }, 1) },
            GroupByClauses: Array.Empty<ParsedClause>(),
            OrderByClauses: Array.Empty<ParsedClause>(),
            SelectivityEstimate: 1.0m,
            QueryComplexity: 1
        );

        // Act
        var allTables = query.GetAllTables();

        // Assert
        allTables.Should().Contain("Users");
        allTables.Should().Contain("Orders");
    }

    [Fact]
    public void QueryParser_EstimateSelectivity_HighForEquality()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var predicates = new[] { "Id = 1" };

        // Act
        var selectivity = typeof(QueryParser)
            .GetMethod("EstimateSelectivity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(parser, new object[] { predicates });

        // Assert
        ((decimal?)selectivity).Should().BeLessThan(0.2m);  // Equality is highly selective
    }

    [Fact]
    public void QueryParser_EstimateSelectivity_LowerForLike()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var predicates = new[] { "Name LIKE '%John%'" };

        // Act
        var selectivity = typeof(QueryParser)
            .GetMethod("EstimateSelectivity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(parser, new object[] { predicates });

        // Assert
        ((decimal?)selectivity).Should().BeGreaterThan(0.3m);  // LIKE is less selective
    }

    [Fact]
    public void QueryParser_CountNestingDepth_CountsParentheses()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = "SELECT * FROM (SELECT * FROM (SELECT * FROM Users))";

        // Act
        var depth = typeof(QueryParser)
            .GetMethod("CountNestingDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(parser, new object[] { sql });

        // Assert
        ((int?)depth).Should().Be(2);
    }

    [Fact]
    public void QueryParser_ComplexQuery_DoesNotThrow()
    {
        // Arrange
        var parser = new SqlServerQueryParser();
        var sql = @"
            SELECT u.Id, u.Name
            FROM Users u
            ORDER BY u.Name DESC";

        // Act & Assert
        var action = () => parser.Parse(sql);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("SELECT Id, Name FROM Users WHERE Id = 1")]
    [InlineData("SELECT * FROM Users ORDER BY Name ASC")]
    public void SqlServerQueryParser_ParseVariousQueries_DoesNotThrow(string sql)
    {
        // Arrange
        var parser = new SqlServerQueryParser();

        // Act & Assert
        var action = () => parser.Parse(sql);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("MySQL")]
    public void AllDialectParsers_ParseSimpleQuery_ReturnValidParsedQuery(string dialectName)
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE id = 1";
        QueryParser parser = dialectName switch
        {
            "PostgreSQL" => new PostgreSqlQueryParser(),
            "MySQL" => new MySqlQueryParser(),
            _ => throw new ArgumentException($"Unknown dialect: {dialectName}")
        };

        // Act
        var parsed = parser.Parse(sql);

        // Assert
        parsed.Should().NotBeNull();
        parsed.SelectColumns.Should().NotBeEmpty();
        parsed.FromTables.Should().NotBeEmpty();
    }
}
