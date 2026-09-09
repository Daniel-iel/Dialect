namespace Dialect.Tests.Cli;

using Dialect.Cli.ErrorHandling;
using Xunit;

/// <summary>
/// Tests for the DiagnosticService.
/// Validates error handling, warnings, and context-aware messages.
/// </summary>
public class DiagnosticServiceTests
{
    [Fact]
    public void AddError_WithBasicInfo_CreatesErrorDiagnostic()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddError("TEST_ERROR", "Test error message", "file.cs", 42, 15);
        
        // Assert
        Assert.Single(service.Diagnostics);
        var diagnostic = service.Diagnostics.First();
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TEST_ERROR", diagnostic.Code);
        Assert.Equal("Test error message", diagnostic.Message);
        Assert.Equal("file.cs", diagnostic.FilePath);
        Assert.Equal(42, diagnostic.LineNumber);
        Assert.Equal(15, diagnostic.ColumnNumber);
    }

    [Fact]
    public void AddWarning_WithSuggestion_IncludesSuggestion()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddWarning("TEST_WARN", "Test warning", "Use parameterized queries instead", "file.cs", 10);
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Use parameterized queries instead", diagnostic.Suggestion);
    }

    [Fact]
    public void AddInfo_CreatesInfoDiagnostic()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddInfo("INFO_TEST", "Informational message");
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
    }

    [Fact]
    public void AddUnsupportedFeature_IncludesFeatureName()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddUnsupportedFeature("MERGE", "MERGE statements require manual review", "orders.cs", 50, "Use INSERT...ON CONFLICT instead");
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal("UNSUPPORTED_FEATURE", diagnostic.Code);
        Assert.Contains("MERGE", diagnostic.Message);
        Assert.Equal("MERGE", diagnostic.Context["Feature"]);
    }

    [Fact]
    public void AddDynamicSqlDetected_IncludesContext()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddDynamicSqlDetected("repository.cs", 25, "string interpolation in SQL");
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal("DYNAMIC_SQL_DETECTED", diagnostic.Code);
        Assert.Equal("repository.cs", diagnostic.FilePath);
        Assert.Equal(25, diagnostic.LineNumber);
        Assert.Contains("Manually refactor", diagnostic.Suggestion);
    }

    [Fact]
    public void AddParsingFailure_IncludesSqlSnippet()
    {
        // Arrange
        var service = new DiagnosticService();
        var sql = "SELECT * FROM Users WHERE Status = ";
        
        // Act
        service.AddParsingFailure(sql, "query.cs", 30, "Unexpected end of statement");
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal("PARSE_FAILED", diagnostic.Code);
        Assert.Equal(sql, diagnostic.SqlSnippet);
        Assert.Equal("Unexpected end of statement", diagnostic.Context["ParseError"]);
    }

    [Fact]
    public void AddTransformationFailure_IncludesDialect()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        service.AddTransformationFailure("PostgreSQL", "MERGE syntax not supported", "migration.cs", 15);
        
        // Assert
        var diagnostic = service.Diagnostics.First();
        Assert.Equal("TRANSFORMATION_FAILED", diagnostic.Code);
        Assert.Equal("PostgreSQL", diagnostic.Context["TargetDialect"]);
    }

    [Fact]
    public void Errors_ReturnsOnlyErrorDiagnostics()
    {
        // Arrange
        var service = new DiagnosticService();
        service.AddError("E1", "Error 1");
        service.AddWarning("W1", "Warning 1");
        service.AddError("E2", "Error 2");
        service.AddInfo("I1", "Info 1");
        
        // Act
        var errors = service.Errors;
        
        // Assert
        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.Equal(DiagnosticSeverity.Error, e.Severity));
    }

    [Fact]
    public void Warnings_ReturnsOnlyWarningDiagnostics()
    {
        // Arrange
        var service = new DiagnosticService();
        service.AddError("E1", "Error 1");
        service.AddWarning("W1", "Warning 1");
        service.AddWarning("W2", "Warning 2");
        service.AddInfo("I1", "Info 1");
        
        // Act
        var warnings = service.Warnings;
        
        // Assert
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Equal(DiagnosticSeverity.Warning, w.Severity));
    }

    [Fact]
    public void Infos_ReturnsOnlyInfoDiagnostics()
    {
        // Arrange
        var service = new DiagnosticService();
        service.AddError("E1", "Error 1");
        service.AddWarning("W1", "Warning 1");
        service.AddInfo("I1", "Info 1");
        service.AddInfo("I2", "Info 2");
        
        // Act
        var infos = service.Infos;
        
        // Assert
        Assert.Equal(2, infos.Count);
        Assert.All(infos, i => Assert.Equal(DiagnosticSeverity.Info, i.Severity));
    }

    [Fact]
    public void HasErrors_ReturnsTrueWhenErrorsPresent()
    {
        // Arrange
        var service = new DiagnosticService();
        Assert.False(service.HasErrors);
        
        // Act
        service.AddError("E1", "Error message");
        
        // Assert
        Assert.True(service.HasErrors);
    }

    [Fact]
    public void HasIssues_ReturnsTrueForErrorsAndWarnings()
    {
        // Arrange
        var service = new DiagnosticService();
        Assert.False(service.HasIssues);
        
        // Act
        service.AddWarning("W1", "Warning message");
        
        // Assert
        Assert.True(service.HasIssues);
    }

    [Fact]
    public void Clear_RemovesAllDiagnostics()
    {
        // Arrange
        var service = new DiagnosticService();
        service.AddError("E1", "Error");
        service.AddWarning("W1", "Warning");
        service.AddInfo("I1", "Info");
        Assert.Equal(3, service.Diagnostics.Count);
        
        // Act
        service.Clear();
        
        // Assert
        Assert.Empty(service.Diagnostics);
    }

    [Fact]
    public void GetSummary_WithNoDiagnostics_ReturnsPositiveMessage()
    {
        // Arrange
        var service = new DiagnosticService();
        
        // Act
        var summary = service.GetSummary();
        
        // Assert
        Assert.Equal("✅ No issues detected", summary);
    }

    [Fact]
    public void GetSummary_WithMixedDiagnostics_ReturnsCountsSummary()
    {
        // Arrange
        var service = new DiagnosticService();
        service.AddError("E1", "Error 1");
        service.AddError("E2", "Error 2");
        service.AddWarning("W1", "Warning 1");
        service.AddInfo("I1", "Info 1");
        
        // Act
        var summary = service.GetSummary();
        
        // Assert
        Assert.Contains("2 error(s)", summary);
        Assert.Contains("1 warning(s)", summary);
        Assert.Contains("1 info(s)", summary);
    }

    [Fact]
    public void DiagnosticToString_FormatsContextualMessage()
    {
        // Arrange
        var diagnostic = new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Code = "TEST_CODE",
            Message = "Test message",
            FilePath = "file.cs",
            LineNumber = 42
        };
        
        // Act
        var str = diagnostic.ToString();
        
        // Assert
        Assert.Contains("⚠️", str);
        Assert.Contains("TEST_CODE", str);
        Assert.Contains("file.cs:42", str);
        Assert.Contains("Test message", str);
    }

    [Fact]
    public void DiagnosticToString_WithErrorSeverity_ShowsErrorEmoji()
    {
        // Arrange
        var diagnostic = new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Code = "ERROR_CODE",
            Message = "Error message"
        };
        
        // Act
        var str = diagnostic.ToString();
        
        // Assert
        Assert.Contains("❌", str);
    }

    [Fact]
    public void DiagnosticToString_WithoutLocation_FormatsProperly()
    {
        // Arrange
        var diagnostic = new SqlDiagnostic
        {
            Severity = DiagnosticSeverity.Info,
            Code = "INFO_CODE",
            Message = "Informational message"
        };
        
        // Act
        var str = diagnostic.ToString();
        
        // Assert
        Assert.Contains("ℹ️", str);
        Assert.Contains("INFO_CODE", str);
        Assert.Contains("Informational message", str);
    }
}
