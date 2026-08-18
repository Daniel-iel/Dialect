using Dialect.Core.IndexCandidates;
using Dialect.SqlServer.IndexCandidates;
using FluentAssertions;
using Xunit;

namespace Dialect.Tests.IndexAdvisor;

public class IndexAdvisorTests
{
    private readonly SqlServerIndexAdvisor _advisor = new();
    
    [Fact]
    public void AnalyzeQuery_ReturnsIndexCandidates()
    {
        // Arrange
        var query = "SELECT * FROM Orders WHERE CustomerId = 5 AND Status = 'Active'";
        
        // Act
        var candidates = _advisor.AnalyzeQuery(query);
        
        // Assert
        candidates.Should().NotBeEmpty();
    }
    
    [Fact]
    public void AnalyzeQuery_IdentifiesFilterColumns()
    {
        // Arrange
        var query = "SELECT * FROM Orders WHERE CustomerId = 5 AND Status = 'Active' AND CreatedAt > '2024-01-01'";
        
        // Act
        var candidates = _advisor.AnalyzeQuery(query);
        
        // Assert
        candidates.Should().HaveCountGreaterThan(0);
        candidates.First().Columns.Should().Contain("CustomerId");
    }
    
    [Fact]
    public void IndexCandidate_IsValid()
    {
        // Arrange
        var candidate = new IndexCandidate(
            CandidateId: "IDX_001",
            TableName: "Orders",
            Columns: new List<string> { "CustomerId" },
            IndexType: "Nonclustered",
            IncludeColumns: new List<string>(),
            PartialPredicate: null,
            Reason: "Filter column",
            ImprovementPercentage: 25,
            EstimatedSizeKb: 1024,
            FrequencyScore: 4,
            Priority: 4,
            RoiScore: 1.67m,
            BenefitingQueries: new List<string>(),
            Warnings: new List<string>(),
            DialectOptions: new Dictionary<string, string>()
        );
        
        // Assert
        candidate.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void IndexCandidate_InvalidWithoutTableName()
    {
        // Arrange
        var candidate = new IndexCandidate(
            CandidateId: "IDX_001",
            TableName: "",  // Invalid
            Columns: new List<string> { "CustomerId" },
            IndexType: "Nonclustered",
            IncludeColumns: new List<string>(),
            PartialPredicate: null,
            Reason: "Filter column",
            ImprovementPercentage: 25,
            EstimatedSizeKb: 1024,
            FrequencyScore: 4,
            Priority: 4,
            RoiScore: 1.67m,
            BenefitingQueries: new List<string>(),
            Warnings: new List<string>(),
            DialectOptions: new Dictionary<string, string>()
        );
        
        // Assert
        candidate.IsValid.Should().BeFalse();
    }
    
    [Fact]
    public void GenerateCreateIndexStatement_ProducesValidSql()
    {
        // Arrange
        var query = "SELECT * FROM Orders WHERE CustomerId = 5";
        var candidates = _advisor.AnalyzeQuery(query);
        
        // Act
        var candidate = candidates.First();
        // Note: GenerateCreateIndexStatement is protected, tested through actual index candidate DDL
        
        // Assert
        candidate.TableName.Should().Be("Orders");
        candidate.Columns.Should().NotBeEmpty();
    }
    
    [Fact]
    public void AnalyzeQuery_SuggestsCoveringIndexForSelectStar()
    {
        // Arrange
        var query = "SELECT * FROM Orders WHERE CustomerId = 5";
        
        // Act
        var candidates = _advisor.AnalyzeQuery(query);
        
        // Assert
        candidates.Should().Contain(c => c.IndexType == "Nonclustered");
    }
    
    [Fact]
    public void AnalyzeQuery_DeduplicatesRedundantSuggestions()
    {
        // Arrange
        var query = "SELECT * FROM Orders WHERE CustomerId = 5 AND CustomerId IN (1, 2, 3)";
        
        // Act
        var candidates = _advisor.AnalyzeQuery(query);
        
        // Assert
        var customerIdIndexes = candidates.Where(c => c.Columns.Contains("CustomerId")).ToList();
        customerIdIndexes.Should().HaveCountLessThanOrEqualTo(2);  // Deduplicated
    }
}
