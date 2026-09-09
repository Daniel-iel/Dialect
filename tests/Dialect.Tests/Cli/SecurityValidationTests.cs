namespace Dialect.Tests.Cli;

using Dialect.Cli.Security;
using Xunit;
using System.IO;

/// <summary>
/// Phase 7 Part 1: Security tests for path validation and input sanitization.
/// Validates protection against path traversal and malicious inputs.
/// </summary>
public class SecurityValidationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "SecurityTests_" + Guid.NewGuid());

    public SecurityValidationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    #region Path Traversal Prevention

    [Fact]
    public void IsPathSafe_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var subdir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subdir);
        var filePath = Path.Combine(subdir, "file.cs");
        
        // Act
        var result = SecurityValidator.IsPathSafe(filePath, _testDirectory);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPathSafe_WithPathTraversalAttempt_ReturnsFalse()
    {
        // Arrange
        var traversalPath = Path.Combine(_testDirectory, "..", "escaped", "file.cs");
        
        // Act
        var result = SecurityValidator.IsPathSafe(traversalPath, _testDirectory);
        
        // Assert
        Assert.False(result, "Path traversal attack (..) should be rejected");
    }

    [Fact]
    public void IsPathSafe_WithPathOutsideBase_ReturnsFalse()
    {
        // Arrange
        var otherDir = Path.Combine(Path.GetTempPath(), "Other_" + Guid.NewGuid());
        var filePath = Path.Combine(otherDir, "file.cs");
        
        // Act
        var result = SecurityValidator.IsPathSafe(filePath, _testDirectory);
        
        // Assert
        Assert.False(result, "Path outside base directory should be rejected");
    }

    [Fact]
    public void IsPathSafe_WithEmptyPath_ReturnsFalse()
    {
        // Arrange
        
        // Act
        var result = SecurityValidator.IsPathSafe("", _testDirectory);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathSafe_WithNullPath_ReturnsFalse()
    {
        // Arrange
        
        // Act
        var result = SecurityValidator.IsPathSafe(null, _testDirectory);
        
        // Assert
        Assert.False(result);
    }

    #endregion

    #region Directory Access Validation

    [Fact]
    public void IsDirectoryAccessible_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var dir = Path.Combine(_testDirectory, "accessible");
        Directory.CreateDirectory(dir);
        
        // Act
        var result = SecurityValidator.IsDirectoryAccessible(dir);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDirectoryAccessible_WithNonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var dir = Path.Combine(_testDirectory, "nonexistent_" + Guid.NewGuid());
        
        // Act
        var result = SecurityValidator.IsDirectoryAccessible(dir);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDirectoryAccessible_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        
        // Act
        var result = SecurityValidator.IsDirectoryAccessible("");
        
        // Assert
        Assert.False(result);
    }

    #endregion

    #region File Access Validation

    [Fact]
    public void IsFileAccessible_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.cs");
        File.WriteAllText(filePath, "// test");
        
        // Act
        var result = SecurityValidator.IsFileAccessible(filePath);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFileAccessible_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent_" + Guid.NewGuid() + ".cs");
        
        // Act
        var result = SecurityValidator.IsFileAccessible(filePath);
        
        // Assert
        Assert.False(result);
    }

    #endregion

    #region File Name Validation

    [Fact]
    public void IsFileNameSafe_WithSimpleFileName_ReturnsTrue()
    {
        // Arrange
        var fileName = "UserRepository.cs";
        
        // Act
        var result = SecurityValidator.IsFileNameSafe(fileName);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFileNameSafe_WithPathSeparator_ReturnsFalse()
    {
        // Arrange
        var fileName = "folder/UserRepository.cs";
        
        // Act
        var result = SecurityValidator.IsFileNameSafe(fileName);
        
        // Assert
        Assert.False(result, "File name should not contain path separators");
    }

    [Fact]
    public void IsFileNameSafe_WithTraversal_ReturnsFalse()
    {
        // Arrange
        var fileName = "../UserRepository.cs";
        
        // Act
        var result = SecurityValidator.IsFileNameSafe(fileName);
        
        // Assert
        Assert.False(result, "File name should not contain path traversal");
    }

    [Fact]
    public void IsFileNameSafe_WithInvalidCharacters_ReturnsFalse()
    {
        // Arrange
        var fileName = "User<Repository>.cs";
        
        // Act
        var result = SecurityValidator.IsFileNameSafe(fileName);
        
        // Assert
        Assert.False(result, "File name should not contain invalid characters");
    }

    #endregion

    #region Glob Pattern Validation

    [Fact]
    public void IsGlobPatternSafe_WithSimplePattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "*.cs";
        
        // Act
        var result = SecurityValidator.IsGlobPatternSafe(pattern);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGlobPatternSafe_WithRecursivePattern_ReturnsTrue()
    {
        // Arrange
        var pattern = "**/*.cs";
        
        // Act
        var result = SecurityValidator.IsGlobPatternSafe(pattern);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGlobPatternSafe_WithPathTraversal_ReturnsFalse()
    {
        // Arrange
        var pattern = "../../sensitive/*.cs";
        
        // Act
        var result = SecurityValidator.IsGlobPatternSafe(pattern);
        
        // Assert
        Assert.False(result, "Glob pattern should not contain path traversal");
    }

    [Fact]
    public void IsGlobPatternSafe_WithAbsolutePath_ReturnsFalse()
    {
        // Arrange
        var pattern = Path.IsPathRooted("/root/file.cs") ? "/root/file.cs" : "C:\\root\\file.cs";
        
        // Act
        var result = SecurityValidator.IsGlobPatternSafe(pattern);
        
        // Assert
        Assert.False(result, "Glob pattern should not be absolute path");
    }

    #endregion

    #region SQL Sanitization for Logging

    [Fact]
    public void SanitizeSqlForLogging_WithNormalSql_PreservesContent()
    {
        // Arrange
        var sql = "SELECT * FROM Users WHERE Id = 1";
        
        // Act
        var result = SecurityValidator.SanitizeSqlForLogging(sql);
        
        // Assert
        Assert.Equal(sql, result);
    }

    [Fact]
    public void SanitizeSqlForLogging_WithPassword_MasksCredentials()
    {
        // Arrange
        var sql = "SELECT * FROM Users WHERE password = 'secretpassword'";
        
        // Act
        var result = SecurityValidator.SanitizeSqlForLogging(sql);
        
        // Assert
        Assert.DoesNotContain("secretpassword", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void SanitizeSqlForLogging_WithApiKey_MasksCredentials()
    {
        // Arrange
        var sql = "SELECT * FROM Tokens WHERE api_key = 'sk-1234567890abcdef'";
        
        // Act
        var result = SecurityValidator.SanitizeSqlForLogging(sql);
        
        // Assert
        Assert.DoesNotContain("sk-1234567890abcdef", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void SanitizeSqlForLogging_WithVeryLongSql_Truncates()
    {
        // Arrange
        var sql = new string('A', 1000);
        
        // Act
        var result = SecurityValidator.SanitizeSqlForLogging(sql);
        
        // Assert
        Assert.True(result.Length <= 500, "Long SQL should be truncated");
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void SanitizeSqlForLogging_WithEmptySql_ReturnsEmpty()
    {
        // Arrange
        
        // Act
        var result = SecurityValidator.SanitizeSqlForLogging("");
        
        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Pattern List Validation

    [Fact]
    public void GetSafeGlobPatterns_WithValidPatterns_ReturnsAll()
    {
        // Arrange
        var input = "*.cs, **/*.sql, src/**/*.json";
        
        // Act
        var result = SecurityValidator.GetSafeGlobPatterns(input);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("*.cs", result);
        Assert.Contains("**/*.sql", result);
        Assert.Contains("src/**/*.json", result);
    }

    [Fact]
    public void GetSafeGlobPatterns_WithMixedPatterns_FiltersUnsafe()
    {
        // Arrange
        var input = "*.cs, ../../dangerous.sql, src/**/*.json";
        
        // Act
        var result = SecurityValidator.GetSafeGlobPatterns(input);
        
        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("*.cs", result);
        Assert.Contains("src/**/*.json", result);
        Assert.DoesNotContain("../../dangerous.sql", result);
    }

    [Fact]
    public void GetSafeGlobPatterns_WithEmptyInput_ReturnsEmpty()
    {
        // Arrange
        
        // Act
        var result = SecurityValidator.GetSafeGlobPatterns("");
        
        // Assert
        Assert.Empty(result);
    }

    #endregion

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
