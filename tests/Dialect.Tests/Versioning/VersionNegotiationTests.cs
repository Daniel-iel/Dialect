namespace Dialect.Tests.Versioning;

using FluentAssertions;
using Xunit;
using Dialect.Core.Versioning;
using Dialect.Core.Dialects;
using Dialect.SqlServer;
using Dialect.PostgreSql;
using Dialect.MySql;

/// <summary>
/// Tests for database version detection and capability negotiation across all SQL dialects.
/// </summary>
public class VersionNegotiationTests
{
    // ========== DatabaseVersion Tests ==========

    [Theory]
    [InlineData("2019")]
    [InlineData("13.2")]
    [InlineData("8.0.15")]
    public void DatabaseVersion_Parse_HandlesValidFormats(string versionString)
    {
        // Act
        var version = DatabaseVersion.Parse(versionString);

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("a.b.c")]
    public void DatabaseVersion_Parse_ThrowsOnInvalidFormat(string invalidFormat)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DatabaseVersion.Parse(invalidFormat));
    }

    [Fact]
    public void DatabaseVersion_CompareTo_CorrectlyCompares()
    {
        // Arrange
        var v1 = new DatabaseVersion(8, 0, 0);
        var v2 = new DatabaseVersion(8, 0, 15);
        var v3 = new DatabaseVersion(9, 0, 0);

        // Act & Assert
        v1.CompareTo(v2).Should().BeLessThan(0);      // v1 < v2
        v2.CompareTo(v1).Should().BeGreaterThan(0);   // v2 > v1
        v1.CompareTo(v1).Should().Be(0);              // v1 == v1
        v1.CompareTo(v3).Should().BeLessThan(0);      // v1 < v3
    }

    [Fact]
    public void DatabaseVersion_IsAtLeast_WorksCorrectly()
    {
        // Arrange
        var v1 = new DatabaseVersion(8, 0, 15);
        var minimumV = new DatabaseVersion(8, 0, 0);

        // Act & Assert
        v1.IsAtLeast(minimumV).Should().BeTrue();
        v1.IsAtLeast(new DatabaseVersion(8, 0, 20)).Should().BeFalse();
        v1.IsAtLeast(v1).Should().BeTrue();
    }

    [Fact]
    public void DatabaseVersion_ToString_FormatsCorrectly()
    {
        // Arrange
        var version = new DatabaseVersion(2019, 0, 0);

        // Act
        var versionString = version.ToString();

        // Assert
        versionString.Should().Be("2019.0.0");
    }

    // ========== VersionCapabilities Tests ==========

    [Fact]
    public void VersionCapabilities_Supports_RecognizesCapabilities()
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

        // Act & Assert
        capabilities.Supports("WINDOW_FUNCTIONS").Should().BeTrue();
        capabilities.Supports("CTES", "UPSERT").Should().BeTrue();
        capabilities.Supports("JSON").Should().BeTrue();
    }

    [Fact]
    public void VersionCapabilities_Supports_ThrowsOnUnknownCapability()
    {
        // Arrange
        var capabilities = new VersionCapabilities(
            false, false, false, false, false, false, false, false, false, false
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => capabilities.Supports("UNKNOWN_CAPABILITY"));
    }

    // ========== SQL Server Version Detection Tests ==========

    [Fact]
    public void SqlServerVersionDetector_CreatedByDialect()
    {
        // Arrange
        var dialect = new SqlServerDialect();

        // Act
        var detector = dialect.CreateVersionDetector();

        // Assert
        detector.Should().NotBeNull();
        detector.Should().BeOfType<Dialect.SqlServer.Versioning.SqlServerVersionDetector>();
    }

    [Fact]
    public void SqlServerVersionDetector_GetCapabilities_SqlServer2019_SupportsAllFeatures()
    {
        // Arrange
        var detector = new Dialect.SqlServer.Versioning.SqlServerVersionDetector();
        var version = new DatabaseVersion(2019, 0, 0);

        // Act
        var capabilities = detector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsUpsert.Should().BeTrue();
        capabilities.SupportsPartialIndexes.Should().BeTrue();
    }

    // ========== PostgreSQL Version Detection Tests ==========

    [Fact]
    public void PostgreSqlVersionDetector_CreatedByDialect()
    {
        // Arrange
        var dialect = new PostgreSqlDialect();

        // Act
        var detector = dialect.CreateVersionDetector();

        // Assert
        detector.Should().NotBeNull();
        detector.Should().BeOfType<Dialect.PostgreSql.Versioning.PostgreSqlVersionDetector>();
    }

    [Fact]
    public void PostgreSqlVersionDetector_GetCapabilities_PostgreSQL13_SupportsAllFeatures()
    {
        // Arrange
        var detector = new Dialect.PostgreSql.Versioning.PostgreSqlVersionDetector();
        var version = new DatabaseVersion(13, 0, 0);

        // Act
        var capabilities = detector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsGeneratedColumns.Should().BeTrue();
        capabilities.SupportsPartialIndexes.Should().BeTrue();
    }

    [Fact]
    public void PostgreSqlVersionDetector_GetCapabilities_PostgreSQL10_LacksSomeFeatures()
    {
        // Arrange
        var detector = new Dialect.PostgreSql.Versioning.PostgreSqlVersionDetector();
        var version = new DatabaseVersion(10, 0, 0);

        // Act
        var capabilities = detector.GetCapabilities(version);

        // Assert
        capabilities.SupportsGeneratedColumns.Should().BeFalse();
        capabilities.SupportsPartitioning.Should().BeTrue();
    }

    // ========== MySQL Version Detection Tests ==========

    [Fact]
    public void MySqlVersionDetector_CreatedByDialect()
    {
        // Arrange
        var dialect = new MySqlDialect();

        // Act
        var detector = dialect.CreateVersionDetector();

        // Assert
        detector.Should().NotBeNull();
        detector.Should().BeOfType<Dialect.MySql.Versioning.MySqlVersionDetector>();
    }

    [Fact]
    public void MySqlVersionDetector_GetCapabilities_MySQL8_SupportsModernFeatures()
    {
        // Arrange
        var detector = new Dialect.MySql.Versioning.MySqlVersionDetector();
        var version = new DatabaseVersion(8, 0, 0);

        // Act
        var capabilities = detector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeTrue();
        capabilities.SupportsCTEs.Should().BeTrue();
        capabilities.SupportsRecursiveCTEs.Should().BeTrue();
        capabilities.SupportsPartialIndexes.Should().BeFalse();  // MySQL-specific limitation
    }

    [Fact]
    public void MySqlVersionDetector_GetCapabilities_MySQL57_LacksModernFeatures()
    {
        // Arrange
        var detector = new Dialect.MySql.Versioning.MySqlVersionDetector();
        var version = new DatabaseVersion(5, 7, 0);

        // Act
        var capabilities = detector.GetCapabilities(version);

        // Assert
        capabilities.SupportsWindowFunctions.Should().BeFalse();  // Added in 8.0
        capabilities.SupportsCTEs.Should().BeFalse();              // Added in 8.0
        capabilities.SupportsUpsert.Should().BeTrue();
    }

    // ========== Integration Tests ==========

    [Fact]
    public void AllDialects_CreateVersionDetector_ReturnsValidDetector()
    {
        // Arrange
        var dialects = new ISqlDialect[]
        {
            new SqlServerDialect(),
            new PostgreSqlDialect(),
            new MySqlDialect()
        };

        // Act & Assert
        foreach (var dialect in dialects)
        {
            var detector = dialect.CreateVersionDetector();
            detector.Should().NotBeNull();
            detector.DetectedVersion.Should().NotBeNull();
            detector.GetDefaultCapabilities().Should().NotBeNull();
        }
    }

    [Fact]
    public void VersionDetector_TryDetectVersion_ReturnsNullableVersion()
    {
        // Arrange
        var dialect = new SqlServerDialect();
        var detector = dialect.CreateVersionDetector();

        // Act
        var version = detector.TryDetectVersion(dialect);

        // Assert
        version.Should().NotBeNull();  // MVP returns default
        version?.Major.Should().BeGreaterThan(0);
    }

    [Fact]
    public void VersionCapabilities_AllConstructorParameters_AreMapped()
    {
        // Arrange & Act
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

        // Assert
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
