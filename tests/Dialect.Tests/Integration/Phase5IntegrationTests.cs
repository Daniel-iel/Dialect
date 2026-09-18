namespace Dialect.Tests.Integration;

using FluentAssertions;
using Xunit;
using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Schema;
using Dialect.Core.Indexing;
using Dialect.Core.Versioning;
using Dialect.SqlServer;
using Dialect.PostgreSql;
using Dialect.MySql;

/// <summary>
/// End-to-end integration tests combining Schema Validation, Index Advisor, and Version Negotiation.
/// Validates that Phase 5 subsystems work together correctly across all SQL dialects.
/// </summary>
public class Phase5IntegrationTests
{
    private readonly ISqlDialect[] _allDialects = new ISqlDialect[]
    {
        new SqlServerDialect(),
        new PostgreSqlDialect(),
        new MySqlDialect()
    };

    // ========== Schema Validation + Version Negotiation ==========

    [Fact]
    public void SchemaValidator_WithVersionDetector_BothAvailable()
    {
        // Arrange
        foreach (var dialect in _allDialects)
        {
            // Act
            var validator = dialect.CreateSchemaValidator();
            var detector = dialect.CreateVersionDetector();

            // Assert
            validator.Should().NotBeNull();
            detector.Should().NotBeNull();
            detector.DetectedVersion.Major.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void VersionDetector_VersionCapabilities_MapAllFeatures()
    {
        // Arrange
        var sqlServer = new SqlServerDialect();
        var detectorSql = sqlServer.CreateVersionDetector();
        var capSql = detectorSql.GetCapabilities(new DatabaseVersion(2019, 0, 0));

        var postgres = new PostgreSqlDialect();
        var detectorPg = postgres.CreateVersionDetector();
        var capPg = detectorPg.GetCapabilities(new DatabaseVersion(13, 0, 0));

        var mysql = new MySqlDialect();
        var detectorMy = mysql.CreateVersionDetector();
        var capMy = detectorMy.GetCapabilities(new DatabaseVersion(8, 0, 0));

        // Act & Assert
        capSql.SupportsWindowFunctions.Should().Be(true);
        capPg.SupportsPartialIndexes.Should().Be(true);
        capMy.SupportsRecursiveCTEs.Should().Be(true);
    }

    // ========== Index Advisor + Schema Validation ==========

    [Fact]
    public void IndexAdvisor_WithSchemaValidator_ReturnsConsistentStructure()
    {
        // Arrange
        var query = new CompiledQuery(
            "SELECT * FROM Users WHERE UserId = @id",
            new Dictionary<string, object> { { "@id", 1 } }
        );

        foreach (var dialect in _allDialects)
        {
            // Act
            var advisor = dialect.CreateIndexAdvisor();
            var recommendations = advisor.Analyze(query, dialect);
            var validator = dialect.CreateSchemaValidator();

            // Assert
            recommendations.Should().NotBeNull();
            recommendations.Should().BeAssignableTo<IReadOnlyList<IndexRecommendation>>();
            validator.Should().NotBeNull();
        }
    }

    [Fact]
    public void IndexRecommendation_PriorityCalculation_BasedOnSelectivity()
    {
        // Arrange
        var testAdvisor = new TestAdvisor();

        // Act & Assert
        var highSelectivityScore = testAdvisor.TestScoreIndexBenefit(0.9m, 100, 3);
        var lowSelectivityScore = testAdvisor.TestScoreIndexBenefit(0.1m, 1, 0);

        highSelectivityScore.Should().BeGreaterThan(lowSelectivityScore);
    }

    // ========== All Three Systems Integration ==========

    [Fact]
    public void FullPhase5Workflow_AllSubsystemsWorking()
    {
        // This test validates that all Phase 5 subsystems can be created together
        foreach (var dialect in _allDialects)
        {
            // Arrange - Build components
            var validator = dialect.CreateSchemaValidator();
            var advisor = dialect.CreateIndexAdvisor();
            var detector = dialect.CreateVersionDetector();

            var query = new CompiledQuery(
                "SELECT * FROM Orders WHERE CustomerId = @cid",
                new Dictionary<string, object> { { "@cid", 1 } }
            );

            // Act
            var indexRecommendations = advisor.Analyze(query, dialect);
            var version = detector.TryDetectVersion(dialect);
            var capabilities = detector.GetCapabilities(version ?? new DatabaseVersion(1, 0, 0));

            // Assert - All three subsystems working together
            indexRecommendations.Should().NotBeNull();
            version.Should().NotBeNull();
            capabilities.Should().NotBeNull();
            capabilities.SupportsCTEs.Should().BeTrue();
            capabilities.SupportsUpsert.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MySQL")]
    public void Phase5Components_ExistForDialect(string dialectName)
    {
        // Arrange
        ISqlDialect dialect = dialectName switch
        {
            "SqlServer" => new SqlServerDialect(),
            "PostgreSQL" => new PostgreSqlDialect(),
            "MySQL" => new MySqlDialect(),
            _ => throw new ArgumentException($"Unknown dialect: {dialectName}")
        };

        // Act
        var schemaValidator = dialect.CreateSchemaValidator();
        var indexAdvisor = dialect.CreateIndexAdvisor();
        var versionDetector = dialect.CreateVersionDetector();

        // Assert
        schemaValidator.Should().NotBeNull();
        indexAdvisor.Should().NotBeNull();
        versionDetector.Should().NotBeNull();
    }

    [Fact]
    public void VersionCapabilities_VersionSpecific_FeatureAvailability()
    {
        // Arrange
        var mysql57Detector = new Dialect.MySql.Versioning.MySqlVersionDetector();
        var mysql80Detector = new Dialect.MySql.Versioning.MySqlVersionDetector();

        // Act
        var v57Capabilities = mysql57Detector.GetCapabilities(new DatabaseVersion(5, 7, 0));
        var v80Capabilities = mysql80Detector.GetCapabilities(new DatabaseVersion(8, 0, 0));

        // Assert - 5.7 lacks modern features
        v57Capabilities.SupportsWindowFunctions.Should().BeFalse();
        v57Capabilities.SupportsCTEs.Should().BeFalse();

        // 8.0 has them
        v80Capabilities.SupportsWindowFunctions.Should().BeTrue();
        v80Capabilities.SupportsCTEs.Should().BeTrue();
    }

    [Fact]
    public void SchemaValidator_NamingConvention_VariesByDialect()
    {
        // Arrange
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();
        var mysql = new MySqlDialect();

        // Act
        var sqlServerValidator = sqlServer.CreateSchemaValidator();
        var postgresValidator = postgres.CreateSchemaValidator();
        var mysqlValidator = mysql.CreateSchemaValidator();

        // Assert - Different naming conventions
        sqlServerValidator.NamingConvention.Should().Be(NamingConvention.PascalCase);
        postgresValidator.NamingConvention.Should().Be(NamingConvention.SnakeCase);
        mysqlValidator.NamingConvention.Should().Be(NamingConvention.SnakeCase);
    }

    [Fact]
    public void SchemaValidator_MaxIdentifierLength_VariesByDialect()
    {
        // Arrange
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();
        var mysql = new MySqlDialect();

        // Act
        var sqlServerValidator = sqlServer.CreateSchemaValidator();
        var postgresValidator = postgres.CreateSchemaValidator();
        var mysqlValidator = mysql.CreateSchemaValidator();

        // Assert - Different limits
        sqlServerValidator.MaxIdentifierLength.Should().Be(128);
        postgresValidator.MaxIdentifierLength.Should().Be(63);
        mysqlValidator.MaxIdentifierLength.Should().Be(64);
    }

    [Fact]
    public void AllPhase5Subsystems_CreatedWithoutExceptions()
    {
        // Arrange & Act & Assert
        foreach (var dialect in _allDialects)
        {
            // Should not throw
            var validator = dialect.CreateSchemaValidator();
            var advisor = dialect.CreateIndexAdvisor();
            var detector = dialect.CreateVersionDetector();

            validator.Should().NotBeNull();
            advisor.Should().NotBeNull();
            detector.Should().NotBeNull();
        }
    }

    [Fact]
    public void Phase5_BackwardCompatibilityWithPreviousPhases()
    {
        // Ensure Phase 1-4 test infrastructure still works alongside Phase 5
        foreach (var dialect in _allDialects)
        {
            // These should still work
            var queryRenderer = dialect.CreateQueryRenderer();
            var migrationRenderer = dialect.CreateMigrationRenderer();

            // And new Phase 5 methods should also work
            var validator = dialect.CreateSchemaValidator();
            var advisor = dialect.CreateIndexAdvisor();
            var detector = dialect.CreateVersionDetector();

            queryRenderer.Should().NotBeNull();
            migrationRenderer.Should().NotBeNull();
            validator.Should().NotBeNull();
            advisor.Should().NotBeNull();
            detector.Should().NotBeNull();
        }
    }

    [Fact]
    public void AllDialects_ProvidesCompletePhase5API()
    {
        // Verify complete Phase 5 API is available on all dialects
        foreach (var dialect in _allDialects)
        {
            var hasMethods =
                dialect.GetType().GetMethod("CreateSchemaValidator") != null &&
                dialect.GetType().GetMethod("CreateIndexAdvisor") != null &&
                dialect.GetType().GetMethod("CreateVersionDetector") != null;

            hasMethods.Should().BeTrue();
        }
    }

    [Fact]
    public void DatabaseVersion_Comparison_Works()
    {
        // Arrange
        var v1 = new DatabaseVersion(8, 0, 0);
        var v2 = new DatabaseVersion(8, 0, 15);
        var v3 = new DatabaseVersion(9, 0, 0);

        // Act & Assert
        v1.CompareTo(v2).Should().BeLessThan(0);
        v2.CompareTo(v1).Should().BeGreaterThan(0);
        v1.CompareTo(v1).Should().Be(0);
        v1.CompareTo(v3).Should().BeLessThan(0);
    }

    [Fact]
    public void VersionCapabilities_SupportsMethod_Works()
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

    /// <summary>
    /// Test helper for accessing protected methods.
    /// </summary>
    private class TestAdvisor : Dialect.Core.Indexing.IndexAdvisor
    {
        public override IReadOnlyList<IndexRecommendation> Analyze(CompiledQuery query, ISqlDialect dialect) =>
            new List<IndexRecommendation>();

        public decimal TestScoreIndexBenefit(decimal selectivity, int frequency, int joinDepth) =>
            ScoreIndexBenefit(selectivity, frequency, joinDepth);
    }
}
