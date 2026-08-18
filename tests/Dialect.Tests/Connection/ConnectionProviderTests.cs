namespace Dialect.Tests.Connection;

using FluentAssertions;
using Xunit;
using Dialect.Core.Connection;
using Dialect.Core.Dialects;
using Dialect.SqlServer.Connection;
using Dialect.SqlServer;
using Dialect.PostgreSql.Connection;
using Dialect.PostgreSql;
using Dialect.MySql.Connection;
using Dialect.MySql;

/// <summary>
/// Tests for IDbConnectionProvider implementations.
/// Tests connection validation and basic operations without actual database.
/// </summary>
public class ConnectionProviderTests
{
    // ========== SqlServerConnectionProvider Tests ==========

    [Fact]
    public void SqlServerConnectionProvider_HasDialect_ReturnsSqlServerDialect()
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var dialect = provider.Dialect;

        // Assert
        dialect.Should().NotBeNull();
        dialect.Should().BeOfType<SqlServerDialect>();
    }

    [Theory]
    [InlineData("Server=localhost;Database=TestDb;Integrated Security=true;")]
    [InlineData("Data Source=.;Initial Catalog=TestDb;Integrated Security=SSPI;")]
    public void SqlServerConnectionProvider_ValidateConnectionString_AcceptsValidStrings(string connStr)
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a connection string")]
    [InlineData("Database=TestDb;")]  // Missing server
    public void SqlServerConnectionProvider_ValidateConnectionString_RejectsInvalidStrings(string? connStr)
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr ?? string.Empty);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void SqlServerConnectionProvider_GetVersionQuery_ReturnsTSqlQuery()
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var query = provider.GetVersionQuery();

        // Assert
        query.Should().NotBeNullOrWhiteSpace();
        query.Should().Contain("@@VERSION");
    }

    [Theory]
    [InlineData("Microsoft SQL Server 2019 (RTM) - 15.0.2000.5 (X64)", 2019, 0, 2000)]
    [InlineData("Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64)", 2022, 0, 1000)]
    public void SqlServerConnectionProvider_ParseVersion_ParsesVersionCorrectly(
        string versionString, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var version = provider.ParseVersion(versionString);

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(expectedMajor);
        version.Patch.Should().Be(expectedPatch);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Unknown version format")]
    public void SqlServerConnectionProvider_ParseVersion_ReturnsNullForInvalid(string? versionString)
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();

        // Act
        var version = provider.ParseVersion(versionString ?? string.Empty);

        // Assert
        version.Should().BeNull();
    }

    // ========== PostgreSqlConnectionProvider Tests ==========

    [Fact]
    public void PostgreSqlConnectionProvider_HasDialect_ReturnsPostgreSqlDialect()
    {
        // Arrange
        var provider = new PostgreSqlConnectionProvider();

        // Act
        var dialect = provider.Dialect;

        // Assert
        dialect.Should().NotBeNull();
        dialect.Should().BeOfType<PostgreSqlDialect>();
    }

    [Theory]
    [InlineData("Host=localhost;Database=testdb;Username=postgres;Password=password;")]
    [InlineData("Server=127.0.0.1;Port=5432;User Id=postgres;Password=pwd;")]
    public void PostgreSqlConnectionProvider_ValidateConnectionString_AcceptsValidStrings(string connStr)
    {
        // Arrange
        var provider = new PostgreSqlConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Database=testdb;")]  // Missing host
    public void PostgreSqlConnectionProvider_ValidateConnectionString_RejectsInvalidStrings(string? connStr)
    {
        // Arrange
        var provider = new PostgreSqlConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr ?? string.Empty);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void PostgreSqlConnectionProvider_GetVersionQuery_ReturnsAnsiQuery()
    {
        // Arrange
        var provider = new PostgreSqlConnectionProvider();

        // Act
        var query = provider.GetVersionQuery();

        // Assert
        query.Should().NotBeNullOrWhiteSpace();
        query.Should().Contain("version");
    }

    [Theory]
    [InlineData("PostgreSQL 13.2 (Debian 13.2-1) on x86_64-pc-linux-gnu", 13, 2, 0)]
    [InlineData("PostgreSQL 14.5 on x86_64-pc-linux-gnu, compiled by gcc", 14, 5, 0)]
    public void PostgreSqlConnectionProvider_ParseVersion_ParsesVersionCorrectly(
        string versionString, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        // Arrange
        var provider = new PostgreSqlConnectionProvider();

        // Act
        var version = provider.ParseVersion(versionString);

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
        version.Patch.Should().Be(expectedPatch);
    }

    // ========== MySqlConnectionProvider Tests ==========

    [Fact]
    public void MySqlConnectionProvider_HasDialect_ReturnsMySqlDialect()
    {
        // Arrange
        var provider = new MySqlConnectionProvider();

        // Act
        var dialect = provider.Dialect;

        // Assert
        dialect.Should().NotBeNull();
        dialect.Should().BeOfType<MySqlDialect>();
    }

    [Theory]
    [InlineData("Server=localhost;Database=testdb;Uid=root;Pwd=password;")]
    [InlineData("Data Source=127.0.0.1;Initial Catalog=testdb;User Id=root;Password=pwd;")]
    public void MySqlConnectionProvider_ValidateConnectionString_AcceptsValidStrings(string connStr)
    {
        // Arrange
        var provider = new MySqlConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Database=testdb;")]  // Missing server
    public void MySqlConnectionProvider_ValidateConnectionString_RejectsInvalidStrings(string? connStr)
    {
        // Arrange
        var provider = new MySqlConnectionProvider();

        // Act
        var isValid = provider.ValidateConnectionString(connStr ?? string.Empty);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MySqlConnectionProvider_GetVersionQuery_ReturnsMySqlQuery()
    {
        // Arrange
        var provider = new MySqlConnectionProvider();

        // Act
        var query = provider.GetVersionQuery();

        // Assert
        query.Should().NotBeNullOrWhiteSpace();
        query.Should().Contain("@@version");
    }

    [Theory]
    [InlineData("8.0.23", 8, 0, 23)]
    [InlineData("5.7.32", 5, 7, 32)]
    [InlineData("8.0.23-0ubuntu0.20.04.1", 8, 0, 23)]
    public void MySqlConnectionProvider_ParseVersion_ParsesVersionCorrectly(
        string versionString, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        // Arrange
        var provider = new MySqlConnectionProvider();

        // Act
        var version = provider.ParseVersion(versionString);

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
        version.Patch.Should().Be(expectedPatch);
    }

    // ========== Cross-Provider Tests ==========

    [Fact]
    public void ConnectionProviders_AllImplementIDbConnectionProvider()
    {
        // Arrange
        IDbConnectionProvider sqlServerProvider = new SqlServerConnectionProvider();
        IDbConnectionProvider postgreSqlProvider = new PostgreSqlConnectionProvider();
        IDbConnectionProvider mySqlProvider = new MySqlConnectionProvider();

        // Act & Assert
        sqlServerProvider.Should().NotBeNull();
        postgreSqlProvider.Should().NotBeNull();
        mySqlProvider.Should().NotBeNull();
    }

    [Theory]
    [InlineData(typeof(SqlServerConnectionProvider))]
    [InlineData(typeof(PostgreSqlConnectionProvider))]
    [InlineData(typeof(MySqlConnectionProvider))]
    public void ConnectionProviders_HaveValidVersionQueries(Type providerType)
    {
        // Arrange
        var provider = (IDbConnectionProvider)Activator.CreateInstance(providerType)!;

        // Act
        var query = provider.GetVersionQuery();

        // Assert
        query.Should().NotBeNullOrWhiteSpace();
        query.Should().NotBeEmpty();
    }

    [Fact]
    public void SqlServerConnectionProvider_ParseVersion_HandlesMultipleFormats()
    {
        // Arrange
        var provider = new SqlServerConnectionProvider();
        var versions = new[]
        {
            "15.0.2000.5",
            "Microsoft SQL Server 2019 (RTM) - 15.0.2000.5 (X64)",
            "SQL Server 15.0.2000.5"
        };

        // Act & Assert
        foreach (var versionStr in versions)
        {
            var version = provider.ParseVersion(versionStr);
            version.Should().NotBeNull($"Failed to parse: {versionStr}");
        }
    }
}
