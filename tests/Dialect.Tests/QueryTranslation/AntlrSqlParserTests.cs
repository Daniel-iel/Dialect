namespace Dialect.Tests.QueryTranslation;

using FluentAssertions;
using Xunit;
using Dialect.Core.AST;
using Dialect.Core.Parsing;

/// <summary>
/// Integration tests for AntlrSqlParser (hybrid keyword-based SQL parser).
/// Validates SELECT statement parsing across SQL Server, PostgreSQL, and MySQL dialects.
/// </summary>
public class AntlrSqlParserTests
{
    private readonly ISqlParser _parser = new AntlrSqlParser();

    #region Simple SELECT Tests

    [Fact]
    public void Parse_SimpleSelect_ReturnsSelectStatement()
    {
        // Arrange
        const string sql = "SELECT Id, Name FROM Users";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.Columns.Should().HaveCount(2);
        result.Columns.Should().Contain(c => c.Name == "Id");
        result.Columns.Should().Contain(c => c.Name == "Name");
        result.From.Should().NotBeNull();
        result.From!.Name.Should().Be("Users");
    }

    [Fact]
    public void Parse_SelectAllColumns_ReturnsSelectAll()
    {
        // Arrange
        const string sql = "SELECT * FROM Customers";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.Columns.Should().HaveCount(1);
        result.Columns[0].Name.Should().Be("*");
        result.From!.Name.Should().Be("Customers");
    }

    [Fact]
    public void Parse_SelectWithAlias_ReturnsAliasedColumns()
    {
        // Arrange
        const string sql = "SELECT Id AS UserId, Name AS UserName FROM Users";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.Columns.Should().HaveCount(2);
        result.Columns[0].Name.Should().Be("Id AS UserId");
        result.Columns[1].Name.Should().Be("Name AS UserName");
    }

    [Fact]
    public void Parse_SelectDistinct_SetsDistinctFlag()
    {
        // Arrange
        const string sql = "SELECT DISTINCT Category FROM Products";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.IsDistinct.Should().BeTrue();
        result.Columns.Should().HaveCount(1);
        result.Columns[0].Name.Should().Be("Category");
    }

    #endregion

    #region FROM Clause Tests

    [Fact]
    public void Parse_SelectWithTableAlias_ReturnsTableReference()
    {
        // Arrange
        const string sql = "SELECT u.Id, u.Name FROM Users u";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.From.Should().NotBeNull();
        result.From!.Name.Should().Contain("Users");
    }

    [Fact]
    public void Parse_SelectWithSchemaQualifiedTable_ParsesTableName()
    {
        // Arrange
        const string sql = "SELECT * FROM dbo.Users";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.From.Should().NotBeNull();
        result.From!.Name.Should().Be("dbo.Users");
    }

    #endregion

    #region WHERE Clause Tests

    [Fact]
    public void Parse_SelectWithWhereClause_ParsesSuccessfully()
    {
        // Arrange
        const string sql = "SELECT * FROM Users WHERE Id = 1";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.From!.Name.Should().Be("Users");
        // WHERE clause parsing is simplified for now
    }

    [Fact]
    public void Parse_SelectWithComplexWhere_ParsesWithoutError()
    {
        // Arrange
        const string sql = "SELECT * FROM Orders WHERE Status = 'Pending' AND Amount > 100";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.From!.Name.Should().Be("Orders");
    }

    #endregion

    #region GROUP BY Clause Tests

    [Fact]
    public void Parse_SelectWithGroupBy_ReturnsGroupByColumns()
    {
        // Arrange
        const string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.GroupByColumns.Should().NotBeNull();
        result.GroupByColumns!.Should().HaveCount(1);
        result.GroupByColumns[0].Name.Should().Be("Category");
    }

    [Fact]
    public void Parse_SelectWithMultipleGroupByColumns_ReturnsAllColumns()
    {
        // Arrange
        const string sql = "SELECT Year, Month, Total FROM Sales GROUP BY Year, Month";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.GroupByColumns.Should().NotBeNull();
        result.GroupByColumns!.Should().HaveCount(2);
        result.GroupByColumns[0].Name.Should().Be("Year");
        result.GroupByColumns[1].Name.Should().Be("Month");
    }

    #endregion

    #region ORDER BY Clause Tests

    [Fact]
    public void Parse_SelectWithOrderBy_ReturnsOrderByClause()
    {
        // Arrange
        const string sql = "SELECT * FROM Users ORDER BY Name";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.OrderByClauses.Should().NotBeNull();
        result.OrderByClauses!.Should().HaveCount(1);
        result.OrderByClauses[0].Direction.Should().Be(SortDirection.Ascending);
    }

    [Fact]
    public void Parse_SelectWithOrderByDesc_SetsSortDirection()
    {
        // Arrange
        const string sql = "SELECT * FROM Orders ORDER BY CreatedDate DESC";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.OrderByClauses.Should().NotBeNull();
        result.OrderByClauses!.Should().HaveCount(1);
        result.OrderByClauses[0].Direction.Should().Be(SortDirection.Descending);
    }

    [Fact]
    public void Parse_SelectWithMultipleOrderBy_ReturnsAllColumns()
    {
        // Arrange
        const string sql = "SELECT * FROM Users ORDER BY Department ASC, Name DESC";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.OrderByClauses.Should().NotBeNull();
        result.OrderByClauses!.Should().HaveCount(2);
        result.OrderByClauses[0].Direction.Should().Be(SortDirection.Ascending);
        result.OrderByClauses[1].Direction.Should().Be(SortDirection.Descending);
    }

    #endregion

    #region LIMIT/OFFSET/TOP Tests

    [Fact]
    public void Parse_SelectWithLimit_ReturnsRowLimit()
    {
        // Arrange
        const string sql = "SELECT * FROM Users LIMIT 10";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit.Should().NotBeNull();
        result.RowLimit!.Count.Should().Be(10);
    }

    [Fact]
    public void Parse_SelectWithTop_ReturnsRowLimit()
    {
        // Arrange
        const string sql = "SELECT TOP 5 * FROM Products";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit.Should().NotBeNull();
        result.RowLimit!.Count.Should().Be(5);
    }

    [Fact]
    public void Parse_SelectWithLimitAndOffset_ReturnsRowLimitWithOffset()
    {
        // Arrange
        const string sql = "SELECT * FROM Users LIMIT 20 OFFSET 10";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit.Should().NotBeNull();
        result.RowLimit!.Count.Should().Be(20);
    }

    #endregion

    #region Complex Query Tests

    [Fact]
    public void Parse_ComplexSelectWithMultipleClauses_ParsesSuccessfully()
    {
        // Arrange
        const string sql = @"
            SELECT DISTINCT u.Id, u.Name, COUNT(o.Id) AS OrderCount
            FROM Users u
            WHERE u.Status = 'Active'
            GROUP BY u.Id, u.Name
            ORDER BY OrderCount DESC
            LIMIT 100";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.IsDistinct.Should().BeTrue();
        result.Columns.Should().HaveCount(3);
        result.From.Should().NotBeNull();
        result.GroupByColumns.Should().NotBeNull();
        result.GroupByColumns!.Should().HaveCount(2);
        result.OrderByClauses.Should().NotBeNull();
        result.RowLimit.Should().NotBeNull();
        result.RowLimit!.Count.Should().Be(100);
    }

    [Fact]
    public void Parse_SelectWithSubqueryInColumns_ParsesWithoutError()
    {
        // Arrange
        const string sql = @"SELECT Id, Name, (SELECT COUNT(*) FROM Orders WHERE UserId = Users.Id) AS OrderCount FROM Users";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.Columns.Should().HaveCount(3);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        // Act
        var result = _parser.Parse("", SqlProvider.SqlServer);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NonSelectStatement_ReturnsNull()
    {
        // Arrange
        const string sql = "INSERT INTO Users (Name) VALUES ('John')";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        // Act
        var result = _parser.Parse(null!, SqlProvider.SqlServer);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Dialect-Specific Tests

    [Fact]
    public void Parse_SqlServerSyntax_ParsesSuccessfully()
    {
        // Arrange
        const string sql = "SELECT TOP 10 * FROM [Users] WHERE [Id] = 1";

        // Act
        var result = _parser.Parse(sql, SqlProvider.SqlServer);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit!.Count.Should().Be(10);
    }

    [Fact]
    public void Parse_PostgreSqlSyntax_ParsesSuccessfully()
    {
        // Arrange
        const string sql = "SELECT * FROM users LIMIT 10 OFFSET 5";

        // Act
        var result = _parser.Parse(sql, SqlProvider.PostgreSql);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit.Should().NotBeNull();
        result.RowLimit!.Count.Should().Be(10);
    }

    [Fact]
    public void Parse_MySqlSyntax_ParsesSuccessfully()
    {
        // Arrange
        const string sql = "SELECT * FROM `users` LIMIT 10";

        // Act
        var result = _parser.Parse(sql, SqlProvider.MySql);

        // Assert
        result.Should().NotBeNull();
        result!.RowLimit!.Count.Should().Be(10);
    }

    #endregion
}

/// <summary>
/// Tests for AntlrSqlParserAdapter (SqlParserAdapter implementation).
/// Validates untranslatable construct detection and integration with parser.
/// </summary>
public class AntlrSqlParserAdapterTests
{
    private readonly AntlrSqlParserAdapter _adapter = new();

    #region Untranslatable Construct Detection

    [Fact]
    public void DetectUntranslatableConstructs_DynamicSqlWithInterpolation_DetectsPattern()
    {
        // Arrange
        const string sql = @"SELECT * FROM Users WHERE Id = $""{userId}""";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("Dynamic SQL"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_StringConcatenation_DetectsPattern()
    {
        // Arrange
        const string sql = @"SELECT * FROM Users WHERE Name = 'John' + ' ' + 'Doe'";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("concatenation"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_StoredProcedureCall_DetectsPattern()
    {
        // Arrange
        const string sql = @"EXEC sp_GetUsers @Status = 'Active'";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("stored procedure") || i.Contains("EXEC"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_CursorOperation_DetectsPattern()
    {
        // Arrange
        const string sql = @"DECLARE UserCursor CURSOR FOR SELECT * FROM Users";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("Cursor"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_JsonOperations_DetectsPattern()
    {
        // Arrange
        const string sql = @"SELECT JSON_VALUE(Data, '$.Name') FROM Users";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("JSON"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_FulltextSearch_DetectsPattern()
    {
        // Arrange
        const string sql = @"SELECT * FROM Articles WHERE MATCH(Content) AGAINST('keyword')";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("FULLTEXT"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_XmlOperations_DetectsPattern()
    {
        // Arrange
        const string sql = @"SELECT Data.value('(/root/name)[1]', 'VARCHAR(50)') FROM Users";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("XML"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_ClrProcedure_DetectsPattern()
    {
        // Arrange
        const string sql = @"CREATE PROCEDURE sp_Test EXTERNAL NAME MyAssembly.MyClass.MyMethod";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().NotBeEmpty();
        issues.Should().Contain(i => i.Contains("CLR"));
    }

    [Fact]
    public void DetectUntranslatableConstructs_NoIssues_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT * FROM Users WHERE Id = 1 ORDER BY Name";

        // Act
        var issues = _adapter.DetectUntranslatableConstructs(sql);

        // Assert
        issues.Should().BeEmpty();
    }

    #endregion

    #region ParseToAst Integration Tests

    [Fact]
    public void ParseToAst_ValidSelectStatement_ReturnsAst()
    {
        // Arrange
        const string sql = "SELECT Id, Name FROM Users";

        // Act
        var ast = _adapter.ParseToAst(sql);

        // Assert
        ast.Should().NotBeNull();
        ast!.Columns.Should().HaveCount(2);
    }

    [Fact]
    public void ParseToAst_NonSelectStatement_ReturnsNull()
    {
        // Arrange
        const string sql = "INSERT INTO Users VALUES (1, 'John')";

        // Act
        var ast = _adapter.ParseToAst(sql);

        // Assert
        ast.Should().BeNull();
    }

    #endregion
}
