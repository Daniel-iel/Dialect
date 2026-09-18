namespace Dialect.Tests.Connection;

using FluentAssertions;
using Xunit;
using Dialect.Core.Connection;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.SqlServer.Connection;
using Dialect.SqlServer;
using Dialect.PostgreSql.Connection;
using Dialect.PostgreSql;
using Dialect.PostgreSql.Versioning;
using Dialect.MySql.Connection;
using Dialect.MySql;
using Dialect.MySql.Versioning;
using Moq;

/// <summary>
/// Tests for QueryVersionDetector.
/// Tests version detection workflow without requiring actual databases.
/// </summary>
public class RealVersionDetectorTests
{
    private class MockDbConnectionProvider : IDbConnectionProvider
    {
        private readonly string _versionString;
        private readonly DatabaseVersion? _parsedVersion;

        public ISqlDialect Dialect => new SqlServerDialect();

        public MockDbConnectionProvider(string versionString, DatabaseVersion? parsedVersion = null)
        {
            _versionString = versionString;
            _parsedVersion = parsedVersion;
        }

        public object OpenConnection(string connectionString) => new object();

        public void CloseConnection(object connection) { }

        public string ExecuteScalar(object connection, string query) => _versionString;

        public IReadOnlyList<Dictionary<string, object>> ExecuteQuery(object connection, string query)
            => new List<Dictionary<string, object>>();

        public string GetVersionQuery() => "SELECT @@VERSION";

        public DatabaseVersion? ParseVersion(string versionString)
            => _parsedVersion ?? new DatabaseVersion(2019, 0, 0);

        public bool ValidateConnectionString(string connectionString) => true;
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ReturnsVersionWhenSuccessful()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "Microsoft SQL Server 2019 (RTM)",
            new DatabaseVersion(2019, 0, 2000)
        );
        var detector = new QueryVersionDetector(mockProvider);
        const string connStr = "Server=localhost;Database=test";

        // Act
        var version = detector.DetectVersion(connStr);

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(2019);
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ReturnsNullForInvalidConnectionString()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(false);

        var detector = new QueryVersionDetector(mockProvider.Object);

        // Act
        var version = detector.DetectVersion("invalid connection string");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ReturnsNullWhenConnectionFails()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(true);
        mockProvider
            .Setup(p => p.OpenConnection(It.IsAny<string>()))
            .Throws(new Exception("Connection failed"));

        var detector = new QueryVersionDetector(mockProvider.Object);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ReturnsNullWhenQueryFails()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(true);
        mockProvider
            .Setup(p => p.OpenConnection(It.IsAny<string>()))
            .Returns(new object());
        mockProvider
            .Setup(p => p.GetVersionQuery())
            .Returns("SELECT @@VERSION");
        mockProvider
            .Setup(p => p.ExecuteScalar(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new Exception("Query failed"));

        var detector = new QueryVersionDetector(mockProvider.Object);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ReturnsNullForEmptyVersionString()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "",
            null
        );
        var detector = new QueryVersionDetector(mockProvider);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ClosesConnectionOnSuccess()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(true);

        var mockConnection = new object();
        mockProvider
            .Setup(p => p.OpenConnection(It.IsAny<string>()))
            .Returns(mockConnection);
        mockProvider
            .Setup(p => p.GetVersionQuery())
            .Returns("SELECT @@VERSION");
        mockProvider
            .Setup(p => p.ExecuteScalar(It.IsAny<object>(), It.IsAny<string>()))
            .Returns("2019");
        mockProvider
            .Setup(p => p.ParseVersion(It.IsAny<string>()))
            .Returns(new DatabaseVersion(2019, 0, 0));

        var detector = new QueryVersionDetector(mockProvider.Object);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().NotBeNull();
        mockProvider.Verify(p => p.CloseConnection(mockConnection), Times.Once);
    }

    [Fact]
    public void QueryVersionDetector_DetectVersion_ClosesConnectionOnError()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(true);

        var mockConnection = new object();
        mockProvider
            .Setup(p => p.OpenConnection(It.IsAny<string>()))
            .Returns(mockConnection);
        mockProvider
            .Setup(p => p.GetVersionQuery())
            .Returns("SELECT @@VERSION");
        mockProvider
            .Setup(p => p.ExecuteScalar(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new Exception("Query failed"));

        var detector = new QueryVersionDetector(mockProvider.Object);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().BeNull();
        mockProvider.Verify(p => p.CloseConnection(mockConnection), Times.Once);
    }

    [Fact]
    public void QueryVersionDetector_Constructor_ThrowsIfProviderIsNull()
    {
        // Act & Assert
        var action = () => new QueryVersionDetector(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void QueryVersionDetector_DetectCapabilities_ReturnsCapabilitiesWhenDetectionSucceeds()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "PostgreSQL 13.2",
            new DatabaseVersion(13, 2, 0)
        );
        var detector = new QueryVersionDetector(mockProvider);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("Host=localhost", versionDetector);

        // Assert
        capabilities.Should().NotBeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectCapabilities_ReturnsNullWhenDetectionFails()
    {
        // Arrange
        var mockProvider = new Mock<IDbConnectionProvider>();
        mockProvider
            .Setup(p => p.ValidateConnectionString(It.IsAny<string>()))
            .Returns(false);

        var detector = new QueryVersionDetector(mockProvider.Object);
        var versionDetector = new PostgreSqlVersionDetector();

        // Act
        var capabilities = detector.DetectCapabilities("invalid", versionDetector);

        // Assert
        capabilities.Should().BeNull();
    }

    [Fact]
    public void QueryVersionDetector_DetectCapabilities_ThrowsIfVersionDetectorIsNull()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider("2019");
        var detector = new QueryVersionDetector(mockProvider);

        // Act & Assert
        var action = () => detector.DetectCapabilities("Server=localhost", null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("2019")]
    [InlineData("PostgreSQL 13")]
    [InlineData("8.0.23")]
    public void QueryVersionDetector_DetectVersion_ParsesVariousVersionFormats(string versionString)
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(versionString);
        var detector = new QueryVersionDetector(mockProvider);

        // Act
        var version = detector.DetectVersion("Server=localhost");

        // Assert
        version.Should().NotBeNull();
    }

    [Fact]
    public void SqlServerConnectionProvider_UsedWithQueryVersionDetector_WorksTogether()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "Microsoft SQL Server 2022 (RTM) - 16.0.1000.6",
            new DatabaseVersion(2022, 0, 1000)
        );
        var detector = new QueryVersionDetector(mockProvider);

        // Act
        var version = detector.DetectVersion("Server=localhost;Database=test");

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(2022);
    }

    [Fact]
    public void PostgreSqlConnectionProvider_UsedWithQueryVersionDetector_WorksTogether()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "PostgreSQL 14.5 on x86_64",
            new DatabaseVersion(14, 5, 0)
        );
        var detector = new QueryVersionDetector(mockProvider);

        // Act
        var version = detector.DetectVersion("Host=localhost;Database=test");

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(14);
    }

    [Fact]
    public void MySqlConnectionProvider_UsedWithQueryVersionDetector_WorksTogether()
    {
        // Arrange
        var mockProvider = new MockDbConnectionProvider(
            "8.0.28",
            new DatabaseVersion(8, 0, 28)
        );
        var detector = new QueryVersionDetector(mockProvider);

        // Act
        var version = detector.DetectVersion("Server=localhost;Database=test");

        // Assert
        version.Should().NotBeNull();
        version!.Major.Should().Be(8);
    }
}
