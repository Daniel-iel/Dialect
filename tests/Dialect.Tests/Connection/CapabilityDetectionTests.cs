namespace Dialect.Tests.Connection;

using FluentAssertions;
using Xunit;
using Dialect.Core.Connection;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.SqlServer.Connection;
using Dialect.SqlServer;
using Dialect.SqlServer.Versioning;
using Dialect.PostgreSql.Connection;
using Dialect.PostgreSql;
using Dialect.PostgreSql.Versioning;
using Dialect.MySql.Connection;
using Dialect.MySql;
using Dialect.MySql.Versioning;
using Moq;

/// <summary>
/// Tests for capability detection based on versions.
/// Verifies that detected versions map to correct capabilities.
/// </summary>
public class CapabilityDetectionTests
{
    private class MockVersionProvider : IDbConnectionProvider
    {
        private readonly DatabaseVersion _version;

        public ISqlDialect Dialect => new SqlServerDialect();

        public MockVersionProvider(DatabaseVersion version)
        {
            _version = version;
        }

        public object OpenConnection(string connectionString) => new object();
        public void CloseConnection(object connection) { }
        public string ExecuteScalar(object connection, string query) => _version.ToString();
        public IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query)
            => new List<Dictionary<string, object>>();
        public string GetVersionQuery() => "SELECT @@VERSION";
        public DatabaseVersion? ParseVersion(string versionString) => _version;
        public bool ValidateConnectionString(string connectionString) => true;
    }

    [Fact]
    public void SqlServer2019_HasAllCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(2019, 0, 0);
        var versionDetector = new SqlServerVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsUpsert.Should().BeTrue();
        capabilities.SupportsJsonFunctions.Should().BeTrue();
    }

    [Fact]
    public void SqlServer2017_HasAllCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(2017, 0, 0);
        var versionDetector = new SqlServerVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
    }

    [Fact]
    public void PostgreSql13_HasAllCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(13, 2, 0);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsUpsert.Should().BeTrue();
    }

    [Fact]
    public void PostgreSql12_LacksGeneratedColumns()
    {
        // Arrange
        var version = new DatabaseVersion(12, 0, 0);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.SupportsGeneratedColumns.Should().BeFalse();
    }

    [Fact]
    public void PostgreSql10_LacksMultipleFeatures()
    {
        // Arrange
        var version = new DatabaseVersion(10, 0, 0);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();  // Available in 10
        capabilities.SupportsGeneratedColumns.Should().BeFalse();  // Added in 12
    }

    [Fact]
    public void MySql80_HasAllCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(8, 0, 0);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
    }

    [Fact]
    public void MySql57_LacksWindowFunctions()
    {
        // Arrange
        var version = new DatabaseVersion(5, 7, 0);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeFalse();
        capabilities.SupportsCTEs.Should().BeFalse();
    }

    [Fact]
    public void MySql57_SupportsUpsert()
    {
        // Arrange
        var version = new DatabaseVersion(5, 7, 32);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        // MySQL 5.7 supports INSERT ... ON DUPLICATE KEY UPDATE
        capabilities.SupportsUpsert.Should().BeTrue();
    }

    [Fact]
    public void MySql80_SupportsUpsert()
    {
        // Arrange
        var version = new DatabaseVersion(8, 0, 20);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = versionDetector.GetCapabilities(version);

        // Assert
        capabilities.SupportsUpsert.Should().BeTrue();
    }

    [Fact]
    public void VersionCapabilities_SupportsMethod_ChecksMultipleCapabilities()
    {
        // Arrange
        var capabilities = new VersionCapabilities(
            SupportsWindowFunctions: true,
            SupportsCTEs: true,
            SupportsUpsert: false,
            SupportsJsonFunctions: true,
            SupportsFullTextSearch: false,
            SupportsPartitioning: true,
            SupportsGeneratedColumns: false,
            SupportsCommonTableExpressions: true,
            SupportsPartialIndexes: true,
            SupportsRecursiveCTEs: true
        );

        // Act
        var allSupported = capabilities.Supports("WINDOW_FUNCTIONS", "CTES", "JSON");
        var notAllSupported = capabilities.Supports("WINDOW_FUNCTIONS", "UPSERT", "FULLTEXT");

        // Assert
        allSupported.Should().BeTrue();
        notAllSupported.Should().BeFalse();
    }

    [Fact]
    public void VersionCapabilities_SupportsMethod_EmptyListReturnsTrue()
    {
        // Arrange
        var capabilities = new VersionCapabilities(
            SupportsWindowFunctions: true,
            SupportsCTEs: true,
            SupportsUpsert: true,
            SupportsJsonFunctions: true,
            SupportsFullTextSearch: true,
            SupportsPartitioning: true,
            SupportsGeneratedColumns: true,
            SupportsCommonTableExpressions: true,
            SupportsPartialIndexes: true,
            SupportsRecursiveCTEs: true
        );

        // Act
        var result = capabilities.Supports();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void QueryVersionDetector_WithSqlServer_DetectsFullCapabilities()
    {
        // Arrange
        var mockProvider = new MockVersionProvider(new DatabaseVersion(2019, 0, 0));
        var detector = new QueryVersionDetector(mockProvider);
        var versionDetector = new SqlServerVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("Server=localhost", versionDetector);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities!.SupportsWindowFunctions.Should().BeTrue();
    }

    [Fact]
    public void QueryVersionDetector_WithPostgreSql_DetectsVersionSpecificCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(13, 0, 0);
        var mockProvider = new MockVersionProvider(version);
        var detector = new QueryVersionDetector(mockProvider);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("Host=localhost", versionDetector);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities!.SupportsGeneratedColumns.Should().BeTrue();  // Available in 13
    }

    [Fact]
    public void QueryVersionDetector_WithMySql57_DetectsLimitedCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(5, 7, 32);
        var mockProvider = new MockVersionProvider(version);
        var detector = new QueryVersionDetector(mockProvider);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("Server=localhost", versionDetector);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities!.SupportsWindowFunctions.Should().BeFalse();  // Not available in 5.7
        capabilities.SupportsCTEs.Should().BeFalse();
    }

    [Fact]
    public void QueryVersionDetector_WithMySql80_DetectsFullCapabilities()
    {
        // Arrange
        var version = new DatabaseVersion(8, 0, 23);
        var mockProvider = new MockVersionProvider(version);
        var detector = new QueryVersionDetector(mockProvider);
        var versionDetector = new MySqlVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("Server=localhost", versionDetector);

        // Assert
        capabilities.Should().NotBeNull();
        capabilities!.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
    }

    [Fact]
    public void VersionDetectors_AllProvideDefaultCapabilities()
    {
        // Arrange
        var sqlServerDetector = new SqlServerVersionDetector();
        var postgreSqlDetector = new PostgreSqlVersionDetector();
        var mysqlDetector = new MySqlVersionDetector();

        // Act
        var sqlServerCaps = sqlServerDetector.GetDefaultCapabilities();
        var postgreSqlCaps = postgreSqlDetector.GetDefaultCapabilities();
        var mysqlCaps = mysqlDetector.GetDefaultCapabilities();

        // Assert
        sqlServerCaps.Should().NotBeNull();
        postgreSqlCaps.Should().NotBeNull();
        mysqlCaps.Should().NotBeNull();
    }

    [Fact]
    public void VersionDetectors_DetectedVersionProperty_ReturnsExpectedVersion()
    {
        // Arrange
        var sqlServerDetector = new SqlServerVersionDetector();
        var postgreSqlDetector = new PostgreSqlVersionDetector();
        var mysqlDetector = new MySqlVersionDetector();

        // Act
        var sqlServerVersion = sqlServerDetector.DetectedVersion;
        var postgreSqlVersion = postgreSqlDetector.DetectedVersion;
        var mysqlVersion = mysqlDetector.DetectedVersion;

        // Assert
        sqlServerVersion.Should().NotBeNull();
        postgreSqlVersion.Should().NotBeNull();
        mysqlVersion.Should().NotBeNull();
        sqlServerVersion.Major.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(2022)]
    public void SqlServer_AllRecentVersions_FullSupport(int version)
    {
        // Arrange
        var dbVersion = new DatabaseVersion(version, 0, 0);
        var detector = new SqlServerVersionDetector();

        // Act
        var capabilities = detector.GetCapabilities(dbVersion);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsUpsert.Should().BeTrue();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void PostgreSql_AllRecentVersions_WindowFunctionsSupported(int version)
    {
        // Arrange
        var dbVersion = new DatabaseVersion(version, 0, 0);
        var detector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = detector.GetCapabilities(dbVersion);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
    }

    [Fact]
    public void VersionCapabilities_AllPropertiesAreSetCorrectly()
    {
        // Arrange
        var capabilities = new VersionCapabilities(
            SupportsWindowFunctions: true,
            SupportsCTEs: false,
            SupportsUpsert: true,
            SupportsJsonFunctions: false,
            SupportsFullTextSearch: true,
            SupportsPartitioning: false,
            SupportsGeneratedColumns: true,
            SupportsCommonTableExpressions: false,
            SupportsPartialIndexes: true,
            SupportsRecursiveCTEs: false
        );

        // Act & Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeFalse();
        capabilities.SupportsUpsert.Should().BeTrue();
        capabilities.SupportsJsonFunctions.Should().BeFalse();
        capabilities.SupportsFullTextSearch.Should().BeTrue();
        capabilities.SupportsPartitioning.Should().BeFalse();
        capabilities.SupportsGeneratedColumns.Should().BeTrue();
        capabilities.SupportsCommonTableExpressions.Should().BeFalse();
        capabilities.SupportsPartialIndexes.Should().BeTrue();
        capabilities.SupportsRecursiveCTEs.Should().BeFalse();
    }
}
