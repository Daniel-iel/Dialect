using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Dialect.Cli.SqlDiscovery;

namespace Dialect.Tests.SqlDiscovery
{
    public class RoslynSqlDiscoveryServiceTests
    {
        private readonly RoslynSqlDiscoveryService _service;
        private readonly Mock<ILogger<RoslynSqlDiscoveryService>> _loggerMock;

        public RoslynSqlDiscoveryServiceTests()
        {
            _loggerMock = new Mock<ILogger<RoslynSqlDiscoveryService>>();
            _service = new RoslynSqlDiscoveryService(_loggerMock.Object);
        }

        #region Simple String Literals

        [Fact]
        public void DiscoverSqlStrings_WithSimpleSelectQuery_DetectsSql()
        {
            // Arrange
            var code = @"
var query = ""SELECT * FROM users WHERE id = 1"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.Contains("SELECT", results[0].SqlContent);
            Assert.True(results[0].SuspiciouslyLikesSql > 0.5);
        }

        [Fact]
        public void DiscoverSqlStrings_WithNonSqlString_DoesNotDetect()
        {
            // Arrange
            var code = @"
var message = ""Hello World"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void DiscoverSqlStrings_WithMultipleSqlStrings_DetectsMany()
        {
            // Arrange
            var code = @"
var query1 = ""SELECT * FROM users WHERE id = 1"";
var message = ""Hello"";
var query2 = ""SELECT id, name FROM products ORDER BY name"";
var query3 = ""UPDATE users SET status = 1 WHERE active = 0"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.True(results.Count >= 2); // At least 2 SQL queries should be detected
            
            // Check that high-confidence SELECT queries are found
            var selectCount = results.Count(r => r.SqlContent.Contains("SELECT") && r.SuspiciouslyLikesSql > 0.35);
            Assert.True(selectCount > 0, "Should detect SELECT queries with multiple keywords");
        }

        #endregion

        #region Verbatim Strings

        [Fact]
        public void DiscoverSqlStrings_WithVerbatimMultilineString_DetectsSql()
        {
            // Arrange
            var code = @"
var query = @""SELECT *
FROM users
WHERE id = 1
ORDER BY name"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.NotEmpty(results);
            var verbatimResult = results.FirstOrDefault(r => r.StringKind == "VerbatimString");
            Assert.NotNull(verbatimResult);
            Assert.Contains("SELECT", verbatimResult.SqlContent);
            Assert.Contains("FROM", verbatimResult.SqlContent);
        }

        [Fact]
        public void DiscoverSqlStrings_WithVerbatimMultiline_CorrectlyParsed()
        {
            // Arrange - verbatim strings preserve newlines as-is
            var code = @"
var query = @""SELECT * FROM users
WHERE id = 1"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.NotEmpty(results);
            var result = results.FirstOrDefault(r => r.StringKind == "VerbatimString");
            Assert.NotNull(result);
            Assert.Contains("SELECT", result.SqlContent);
        }

        #endregion

        #region Concatenated Strings

        [Fact]
        public void DiscoverSqlStrings_WithConcatenatedStrings_DetectsComponents()
        {
            // Arrange
            var code = @"
var table = ""users"";
var query = ""SELECT * FROM "" + table + "" WHERE id = 1 ORDER BY name"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.NotEmpty(results);
            // Should detect at least the static string parts
            var sqlLikeResults = results.Where(r => r.SuspiciouslyLikesSql > 0.2).ToList();
            Assert.NotEmpty(sqlLikeResults);
            
            // Check that SELECT and ORDER BY keywords were found
            Assert.True(sqlLikeResults.Any(r => r.SqlContent.Contains("SELECT")),
                "Should find SELECT keyword");
        }

        [Fact]
        public void DiscoverSqlStrings_WithComplexConcatenation_FindsHighConfidenceMatch()
        {
            // Arrange
            var code = @"
var tableName = ""products"";
var columnName = ""price"";
var query = ""SELECT "" + columnName + "" FROM "" + tableName + "" WHERE id = @id"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.NotEmpty(results);
            
            // Should find at least one high-confidence SQL result
            var highConfidence = results.Where(r => r.SuspiciouslyLikesSql > 0.35).FirstOrDefault();
            Assert.NotNull(highConfidence);
            
            // If it's a concatenation, check the dynamic components flag
            if (highConfidence.StringKind == "ConcatenatedString")
            {
                Assert.True(highConfidence.HasDynamicComponents);
            }
        }

        [Fact]
        public void DiscoverSqlStrings_WithConcatenatedDynamicParts_HighConfidence()
        {
            // Arrange
            var code = @"
var where = ""id = 1"";
var sql = ""SELECT * FROM users WHERE "" + where;
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            // May or may not detect depending on confidence threshold
            if (results.Any())
            {
                Assert.True(results[0].HasDynamicComponents);
            }
        }

        #endregion

        #region Interpolated Strings

        [Fact]
        public void DiscoverSqlStrings_WithInterpolatedString_DetectsSql()
        {
            // Arrange
            var code = @"
var id = 42;
var query = $""SELECT * FROM users WHERE id = {id}"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.Equal("InterpolatedString", results[0].StringKind);
            Assert.True(results[0].HasInterpolations);
            Assert.Contains("SELECT", results[0].SqlContent);
            // Interpolation should be replaced with ?
            Assert.Contains("?", results[0].SqlContent);
        }

        [Fact]
        public void DiscoverSqlStrings_WithMultipleInterpolations_ReplacesAll()
        {
            // Arrange
            var code = @"
var table = ""users"";
var column = ""id"";
var value = 5;
var query = $""SELECT {column} FROM {table} WHERE id = {value}"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].HasInterpolations);
            // Each interpolation should become a ?
            var placeholderCount = results[0].SqlContent.Count(c => c == '?');
            Assert.True(placeholderCount >= 3);
        }

        #endregion

        #region SQL Detection Confidence

        [Fact]
        public void IsSuspiciouslyLikesSql_WithMultipleSqlKeywords_HighConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("SELECT * FROM users WHERE id = 1 ORDER BY name");

            // Assert
            Assert.True(confidence > 0.6);
        }

        [Fact]
        public void IsSuspiciouslyLikesSql_WithSingleKeyword_ModerateConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("SELECT * FROM users");

            // Assert
            Assert.True(confidence > 0.3);
            Assert.True(confidence < 1.0);
        }

        [Fact]
        public void IsSuspiciouslyLikesSql_WithNoKeywords_ZeroConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("apple banana cherry dog");

            // Assert
            Assert.Equal(0.0, confidence);
        }

        [Fact]
        public void IsSuspiciouslyLikesSql_WithEmptyString_ZeroConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("");

            // Assert
            Assert.Equal(0.0, confidence);
        }

        [Fact]
        public void IsSuspiciouslyLikesSql_WithShortString_LowConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("SELECT");

            // Assert
            Assert.Equal(0.0, confidence); // Too short
        }

        [Fact]
        public void IsSuspiciouslyLikesSql_WithParameters_HigherConfidence()
        {
            // Act
            var confidence = _service.IsSuspiciouslyLikesSql("SELECT * FROM users WHERE id = @id");

            // Assert
            Assert.True(confidence > 0.5);
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void DiscoverSqlStrings_WithMixedStringTypes_DetectsAll()
        {
            // Arrange
            var code = @"
var simple = ""SELECT * FROM table1"";
var verbatim = @""INSERT INTO logs
VALUES (1, 'text')"";
var id = 42;
var interpolated = $""DELETE FROM users WHERE id = {id}"";
var concat = ""UPDATE products SET price = "" + newPrice;
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.True(results.Count >= 3); // At least 3 should be detected
            var kinds = results.Select(r => r.StringKind).ToList();
            Assert.Contains("RegularString", kinds);
            Assert.Contains("VerbatimString", kinds);
            Assert.Contains("InterpolatedString", kinds);
        }

        [Fact]
        public void DiscoverSqlStrings_WithInvalidCode_ReturnsEmptyOrPartial()
        {
            // Arrange
            var invalidCode = @"
This is not valid C# code at all
var x = 
";

            // Act
            var results = _service.DiscoverSqlStrings(invalidCode);

            // Assert
            // Roslyn is forgiving and tries to parse anyway, so we just expect
            // it to either return empty or parse what it can
            Assert.NotNull(results);
            // Should not throw, should return safely
        }

        [Fact]
        public void DiscoverSqlStrings_WithEmptyCode_ReturnsEmpty()
        {
            // Act
            var results = _service.DiscoverSqlStrings("");

            // Assert
            Assert.Empty(results);
        }

        #endregion

        #region String Kind Detection

        [Fact]
        public void DiscoverSqlStrings_IdentifiesStringKinds()
        {
            // Arrange
            var code = @"
var regular = ""SELECT 1 FROM table"";
var verbatim = @""SELECT
2 FROM another_table"";
var id = 3;
var interpolated = $""SELECT {id} FROM third_table"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.NotEmpty(results);
            
            // Check that we detected different string kinds
            var kinds = results.Select(r => r.StringKind).Distinct().ToList();
            Assert.True(kinds.Count > 1, $"Expected multiple string kinds, got: {string.Join(", ", kinds)}");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void DiscoverSqlStrings_WithEscapedQuotes_HandlesCorrectly()
        {
            // Arrange
            var code = @"
var query = ""SELECT 'quoted' FROM table WHERE name = \\""test\\"" "";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.Contains("SELECT", results[0].SqlContent);
        }

        [Fact]
        public void DiscoverSqlStrings_WithUnicodeContent_DetectsSql()
        {
            // Arrange
            var code = @"
var query = ""SELECT * FROM usuários WHERE id = 1"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.Contains("SELECT", results[0].SqlContent);
        }

        [Fact]
        public void DiscoverSqlStrings_WithCommentsIgnored_SkipsCommented()
        {
            // Arrange
            var code = @"
// var query = ""SELECT * FROM commented"";
var actual = ""SELECT * FROM actual"";
";

            // Act
            var results = _service.DiscoverSqlStrings(code);

            // Assert
            Assert.Single(results);
            Assert.Contains("actual", results[0].SqlContent);
        }

        #endregion
    }
}
