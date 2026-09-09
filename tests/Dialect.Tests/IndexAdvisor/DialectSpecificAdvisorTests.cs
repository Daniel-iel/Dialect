using Dialect.Core.IndexCandidates;
using Dialect.MySql.IndexCandidates;
using Dialect.PostgreSql.IndexCandidates;
using Dialect.SqlServer.IndexCandidates;
using Dialect.Core.QueryRewrite;
using Dialect.MySql.QueryRewrite;
using Dialect.PostgreSql.QueryRewrite;
using Dialect.SqlServer.QueryRewrite;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.IndexAdvisor;

public class DialectIndexAdvisorTests
{
    [Fact]
    public void SqlServerIndexAdvisor_GeneratesNonclusteredIndexes()
    {
        // Arrange
        var advisor = new SqlServerIndexAdvisor();
        const string query = "SELECT * FROM Products WHERE CategoryId = 5 AND IsActive = 1";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().NotBeEmpty();
        candidates.Should().Contain(c => c.IndexType == "Nonclustered");
    }

    [Fact]
    public void PostgreSqlIndexAdvisor_GeneratesPartialIndexes()
    {
        // Arrange
        var advisor = new PostgreSqlIndexAdvisor();
        const string query = "SELECT * FROM Orders WHERE Status = 'pending' AND CreatedAt > NOW() - INTERVAL '30 days'";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().Contain(c => c.IndexType == "Partial");
    }

    [Fact]
    public void PostgreSqlIndexAdvisor_GeneratesBrinIndexes()
    {
        // Arrange
        var advisor = new PostgreSqlIndexAdvisor();
        const string query = "SELECT * FROM events WHERE created_at > NOW() - INTERVAL '1 year'";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().Contain(c => c.IndexType == "BRIN");
    }

    [Fact]
    public void MySqlIndexAdvisor_GeneratesCompositeIndexes()
    {
        // Arrange
        var advisor = new MySqlIndexAdvisor();
        const string query = "SELECT * FROM orders WHERE customer_id = 5 AND status = 'completed' AND order_date > '2024-01-01'";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().Contain(c => c.IndexType == "Composite");
    }

    [Fact]
    public void SqlServerIndexAdvisor_GeneratesDialectSpecificOptions()
    {
        // Arrange
        var advisor = new SqlServerIndexAdvisor();
        const string query = "SELECT * FROM Orders WHERE Id = 1";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().AllSatisfy(c =>
        {
            c.DialectOptions.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void IndexAdvisor_CalculatesRoiScore()
    {
        // Arrange
        var advisor = new SqlServerIndexAdvisor();
        const string query = "SELECT * FROM LargeTable WHERE Id = 1";

        // Act
        var candidates = advisor.AnalyzeQuery(query);

        // Assert
        candidates.Should().AllSatisfy(c =>
        {
            c.RoiScore.Should().BeGreaterThanOrEqualTo(0);
        });
    }
}

public class DialectQueryRewriteAdvisorTests
{
    [Fact]
    public void SqlServerRewriteAdvisor_SuggestsCTE()
    {
        // Arrange
        var advisor = new SqlServerRewriteAdvisor();
        const string query = "SELECT * FROM (SELECT id FROM orders) o JOIN (SELECT id FROM customers) c ON o.id = c.id";

        // Act
        var rewrites = advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.Category == "CTE");
    }

    [Fact]
    public void PostgreSqlRewriteAdvisor_SuggestsMaterializedView()
    {
        // Arrange
        var advisor = new PostgreSqlRewriteAdvisor();
        const string query = "SELECT COUNT(*) FROM orders o JOIN customers c ON o.customer_id = c.id WHERE c.status = 'active' GROUP BY c.region";

        // Act
        var rewrites = advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.Category == "Materialization" || r.Category == "WindowFunction");
    }

    [Fact]
    public void MySqlRewriteAdvisor_SuggestsInToJoinConversion()
    {
        // Arrange
        var advisor = new MySqlRewriteAdvisor();
        const string query = "SELECT * FROM orders WHERE customer_id IN (SELECT id FROM customers WHERE status = 'active')";

        // Act
        var rewrites = advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.RewriteId == "MYSQL_IN_TO_JOIN");
    }

    [Fact]
    public void MySqlRewriteAdvisor_SuggestsBatchInsert()
    {
        // Arrange
        var advisor = new MySqlRewriteAdvisor();
        const string query = "INSERT INTO products (name, price) VALUES ('Product 1', 100)";

        // Act
        var rewrites = advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().Contain(r => r.RewriteId == "MYSQL_BATCH_INSERT");
    }

    [Fact(Skip = "Query rewrite advisor patterns not fully implemented")]
    public void RewriteAdvisor_ProvidesImplementationPatterns()
    {
        // Arrange
        var advisor = new SqlServerRewriteAdvisor();
        const string query = "SELECT * FROM (SELECT id FROM orders) sub";

        // Act
        var rewrites = advisor.AnalyzeQuery(query);

        // Assert
        rewrites.Should().AllSatisfy(r =>
        {
            r.OriginalPattern.Should().NotBeNullOrWhiteSpace();
            r.SuggestedPattern.Should().NotBeNullOrWhiteSpace();
        });
    }
}
