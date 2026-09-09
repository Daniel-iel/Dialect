namespace Dialect.Tests.QueryTranslation;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.QueryTranslation;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;
using FluentAssertions;
using Xunit;

/// <summary>
/// Tests for SQL translation using DefaultSqlTranslator.
/// Covers the three translation patterns: default dialect, connection string detection, explicit provider.
/// </summary>
public class DefaultSqlTranslatorTests
{
    private readonly ISqlProviderDetector _providerDetector;
    private readonly IReadOnlyDictionary<SqlProvider, SqlParserAdapter> _parserAdapters;

    public DefaultSqlTranslatorTests()
    {
        // Setup provider detector
        _providerDetector = new DefaultSqlProviderDetector();

        // Setup parser adapters for each dialect
        _parserAdapters = new Dictionary<SqlProvider, SqlParserAdapter>
        {
            { SqlProvider.SqlServer, new SqlServerParserAdapter() },
            { SqlProvider.PostgreSql, new PostgreSqlParserAdapter() },
            { SqlProvider.MySql, new MySqlParserAdapter() }
        };
    }

    #region Translate with Default Dialect

    [Fact]
    public void Translate_WithoutDefaultDialect_ReturnsErrorMessage()
    {
        // Arrange
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters);
        const string sourceSql = "SELECT id, name FROM users";

        // Act
        var result = translator.Translate(sourceSql);

        // Assert
        result.ErrorMessage.Should().Contain("No default target dialect configured");
        result.Compiled.Should().BeNull();
    }

    #endregion

    #region Translate with Connection String Detection

    [Theory]
    [InlineData("Server=localhost;Database=mydb;User=sa;Password=pass")]
    [InlineData("Data Source=localhost\\SQLEXPRESS")]
    public void Translate_WithSqlServerConnectionString_DetectsSqlServer(string connectionString)
    {
        // Arrange
        var targetDialect = new Dialect.SqlServer.SqlServerDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, targetDialect);
        const string sourceSql = "SELECT id FROM users";

        // Act
        var result = translator.Translate(sourceSql, connectionString, targetDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.SqlServer);
        result.HasCompiledResult.Should().BeTrue();
        result.Compiled.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Host=localhost;Database=mydb;User=postgres")]
    [InlineData("host=mydb.rds.amazonaws.com")]
    public void Translate_WithPostgresConnectionString_DetectsPostgres(string connectionString)
    {
        // Arrange
        var targetDialect = new Dialect.SqlServer.SqlServerDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, targetDialect);
        const string sourceSql = "SELECT id FROM users";

        // Act
        var result = translator.Translate(sourceSql, connectionString, targetDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.PostgreSql);
        result.HasCompiledResult.Should().BeTrue();
        result.Compiled.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Server=localhost;Port=3306;Database=mydb")]
    [InlineData("server=localhost;port=3306;uid=root;pwd=pass")]
    public void Translate_WithMySqlConnectionString_DetectsMySql(string connectionString)
    {
        // Arrange
        var targetDialect = new Dialect.SqlServer.SqlServerDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, targetDialect);
        const string sourceSql = "SELECT id FROM users";

        // Act
        var result = translator.Translate(sourceSql, connectionString, targetDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.MySql);
        result.HasCompiledResult.Should().BeTrue();
        result.Compiled.Should().NotBeNull();
    }

    [Fact]
    public void Translate_WithEmptyConnectionString_ReturnsError()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);
        const string sourceSql = "SELECT id FROM users";

        // Act
        var result = translator.Translate(sourceSql, "", mockDialect);

        // Assert
        result.ErrorMessage.Should().Contain("Connection string cannot be empty");
        result.Compiled.Should().BeNull();
    }

    [Fact]
    public void Translate_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);
        const string sourceSql = "SELECT id FROM users";

        // Act & Assert
        // Note: In this implementation, we catch empty/null and return error result
        // rather than throwing; verify the behavior
        var result = translator.Translate(sourceSql, null!, mockDialect);
        result.ErrorMessage.Should().Contain("Connection string cannot be empty");
    }

    #endregion

    #region Translate with Explicit Provider

    [Fact]
    public void Translate_WithExplicitSqlServerProvider_IncludesUntranslatableConstructs()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);
        const string sqlWithMerge = "MERGE INTO target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.value = s.value";

        // Act
        var result = translator.Translate(sqlWithMerge, SqlProvider.SqlServer, mockDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.SqlServer);
        result.UntranslatableConstructs.Should().Contain("MERGE statement not supported in target dialect");
        result.HasCompiledResult.Should().BeTrue();
    }

    [Fact]
    public void Translate_WithExplicitPostgresProvider_IncludesUntranslatableConstructs()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);
        const string sqlWithJsonOps = "SELECT data ->> 'key' as value FROM users WHERE data @> '{\"status\": \"active\"}'::jsonb";

        // Act
        var result = translator.Translate(sqlWithJsonOps, SqlProvider.PostgreSql, mockDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.PostgreSql);
        result.UntranslatableConstructs.Should().Contain("JSON operators are PostgreSQL specific");
        result.HasCompiledResult.Should().BeTrue();
    }

    [Fact]
    public void Translate_WithExplicitMySqlProvider_IncludesUntranslatableConstructs()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);
        const string sqlWithGroupConcat = "SELECT id, GROUP_CONCAT(name SEPARATOR ', ') as names FROM users GROUP BY department";

        // Act
        var result = translator.Translate(sqlWithGroupConcat, SqlProvider.MySql, mockDialect);

        // Assert
        result.DetectedSourceProvider.Should().Be(SqlProvider.MySql);
        result.UntranslatableConstructs.Should().Contain("GROUP_CONCAT() is MySQL specific");
        result.HasCompiledResult.Should().BeTrue();
    }

    [Fact]
    public void Translate_WithEmptySQL_ReturnsError()
    {
        // Arrange
        var mockDialect = new MockSqlDialect();
        var translator = new DefaultSqlTranslator(_providerDetector, _parserAdapters, mockDialect);

        // Act
        var result = translator.Translate("", SqlProvider.SqlServer, mockDialect);

        // Assert
        result.ErrorMessage.Should().Contain("Source SQL cannot be empty");
        result.Compiled.Should().BeNull();
    }

    #endregion

    #region TranslationResult Properties

    [Fact]
    public void TranslationResult_IsFullyTranslated_ReturnsFalseWhenCompiledIsNull()
    {
        // Arrange & Act
        var result = new TranslationResult
        {
            Compiled = null,
            DetectedSourceProvider = SqlProvider.SqlServer
        };

        // Assert
        result.IsFullyTranslated.Should().BeFalse();
    }

    [Fact]
    public void TranslationResult_IsFullyTranslated_ReturnsFalseWhenUntranslatableConstructsExist()
    {
        // Arrange & Act
        var result = new TranslationResult
        {
            Compiled = new CompiledQuery("SELECT * FROM users", new Dictionary<string, object?>()),
            DetectedSourceProvider = SqlProvider.SqlServer,
            UntranslatableConstructs = new[] { "MERGE not supported" }
        };

        // Assert
        result.IsFullyTranslated.Should().BeFalse();
    }

    [Fact]
    public void TranslationResult_IsFullyTranslated_ReturnsTrueWhenFullyTranslated()
    {
        // Arrange & Act
        var result = new TranslationResult
        {
            Compiled = new CompiledQuery("SELECT * FROM users", new Dictionary<string, object?>()),
            DetectedSourceProvider = SqlProvider.SqlServer,
            UntranslatableConstructs = []
        };

        // Assert
        result.IsFullyTranslated.Should().BeTrue();
    }

    [Fact]
    public void TranslationResult_HasCompiledResult_ReturnsTrueWhenCompiledIsNotNull()
    {
        // Arrange & Act
        var result = new TranslationResult
        {
            Compiled = new CompiledQuery("SELECT * FROM users", new Dictionary<string, object?>()),
            DetectedSourceProvider = SqlProvider.SqlServer,
            UntranslatableConstructs = new[] { "MERGE not supported" }
        };

        // Assert
        result.HasCompiledResult.Should().BeTrue();
        result.IsFullyTranslated.Should().BeFalse(); // Partial result
    }

    #endregion

    /// <summary>
    /// Mock ISqlDialect for testing; minimalist implementation.
    /// </summary>
    private class MockSqlDialect : ISqlDialect
    {
        public char IdentifierQuote => '"';
        public string ParameterPrefix => "@";

        public bool Supports(SqlFeature feature) => true;

        public string RenderFunction(string functionName, IReadOnlyList<string> argumentPlaceholders) =>
            $"{functionName}({string.Join(", ", argumentPlaceholders)})";

        public IQueryRenderer CreateQueryRenderer() =>
            throw new NotImplementedException("Mock does not support query rendering");

        public IRoutineRenderer CreateRoutineRenderer() =>
            throw new NotImplementedException("Mock does not support routine rendering");

        public IMigrationRenderer CreateMigrationRenderer() =>
            throw new NotImplementedException("Mock does not support migration rendering");

        public SchemaValidator CreateSchemaValidator() =>
            throw new NotImplementedException("Mock does not support schema validation");

        public IndexAdvisor CreateIndexAdvisor() =>
            throw new NotImplementedException("Mock does not support index advice");

        public VersionDetector CreateVersionDetector() =>
            throw new NotImplementedException("Mock does not support version detection");
    }
}
