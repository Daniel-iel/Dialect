namespace Dialect.Tests.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;
using Dialect.MySql;
using Dialect.PostgreSql;
using Dialect.SqlServer;
using Xunit;

public class DdlTypeMappingTests
{
    [Fact]
    public void SqlServerToPostgreSql_CreateTable_ShouldMapIdentityAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE [Users] ([Id] INT IDENTITY(1,1), [IsActive] BIT, [CreatedAt] DATETIME2, [UserKey] UNIQUEIDENTIFIER)";
        var result = translator.Translate(sql, SqlProvider.SqlServer, new PostgreSqlDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE \"Users\"", actual);
        Assert.Contains("\"Id\" SERIAL", actual);
        Assert.Contains("\"IsActive\" BOOLEAN", actual);
        Assert.Contains("\"CreatedAt\" TIMESTAMP", actual);
        Assert.Contains("\"UserKey\" UUID", actual);
    }

    [Fact]
    public void SqlServerToMySql_CreateTable_ShouldMapIdentityAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE [Users] ([Id] INT IDENTITY(1,1), [Name] NVARCHAR(100), [UserKey] UNIQUEIDENTIFIER)";
        var result = translator.Translate(sql, SqlProvider.SqlServer, new MySqlDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE `Users`", actual);
        Assert.Contains("`Id` INT AUTO_INCREMENT", actual);
        Assert.Contains("`Name` VARCHAR(100)", actual);
        Assert.Contains("`UserKey` CHAR(36)", actual);
    }

    [Fact]
    public void MySqlToPostgreSql_CreateTable_ShouldMapAutoIncrementAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE `Users` (`Id` INT AUTO_INCREMENT, `Flag` TINYINT(1), `CreatedAt` DATETIME, `UserKey` CHAR(36))";
        var result = translator.Translate(sql, SqlProvider.MySql, new PostgreSqlDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE \"Users\"", actual);
        Assert.Contains("\"Id\" SERIAL", actual);
        Assert.Contains("\"Flag\" BOOLEAN", actual);
        Assert.Contains("\"CreatedAt\" TIMESTAMP", actual);
        Assert.Contains("\"UserKey\" UUID", actual);
    }

    [Fact]
    public void MySqlToSqlServer_CreateTable_ShouldMapAutoIncrementAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE `Users` (`Id` BIGINT AUTO_INCREMENT, `Flag` BOOLEAN, `CreatedAt` TIMESTAMP, `UserKey` CHAR(36))";
        var result = translator.Translate(sql, SqlProvider.MySql, new SqlServerDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE [Users]", actual);
        Assert.Contains("[Id] BIGINT IDENTITY(1,1)", actual);
        Assert.Contains("[Flag] BIT", actual);
        Assert.Contains("[CreatedAt] DATETIME2", actual);
        Assert.Contains("[UserKey] UNIQUEIDENTIFIER", actual);
    }

    [Fact]
    public void PostgreSqlToSqlServer_CreateTable_ShouldMapSerialAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE \"Users\" (\"Id\" SERIAL, \"Flag\" BOOLEAN, \"CreatedAt\" TIMESTAMP, \"UserKey\" UUID)";
        var result = translator.Translate(sql, SqlProvider.PostgreSql, new SqlServerDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE [Users]", actual);
        Assert.Contains("[Id] INT IDENTITY(1,1)", actual);
        Assert.Contains("[Flag] BIT", actual);
        Assert.Contains("[CreatedAt] DATETIME2", actual);
        Assert.Contains("[UserKey] UNIQUEIDENTIFIER", actual);
    }

    [Fact]
    public void PostgreSqlToMySql_CreateTable_ShouldMapSerialAndTypes()
    {
        var translator = CreateTranslator();
        var sql = "CREATE TABLE \"Users\" (\"Id\" BIGSERIAL, \"Flag\" BOOLEAN, \"CreatedAt\" TIMESTAMP, \"UserKey\" UUID)";
        var result = translator.Translate(sql, SqlProvider.PostgreSql, new MySqlDialect());

        Assert.True(result.HasCompiledResult);
        var actual = Normalize(result.Compiled!.Sql);
        Assert.Contains("CREATE TABLE `Users`", actual);
        Assert.Contains("`Id` BIGINT AUTO_INCREMENT", actual);
        Assert.Contains("`Flag` TINYINT(1)", actual);
        Assert.Contains("`CreatedAt` DATETIME", actual);
        Assert.Contains("`UserKey` CHAR(36)", actual);
    }

    private static DefaultSqlTranslator CreateTranslator()
    {
        var detector = new DefaultSqlProviderDetector();
        var adapters = new Dictionary<SqlProvider, SqlParserAdapter>
        {
            [SqlProvider.SqlServer] = new SqlServerParserAdapter(),
            [SqlProvider.PostgreSql] = new PostgreSqlParserAdapter(),
            [SqlProvider.MySql] = new MySqlParserAdapter()
        };

        return new DefaultSqlTranslator(detector, adapters);
    }

    private static string Normalize(string sql) => sql.Trim().TrimEnd(';').Replace("\r\n", "\n");
}
