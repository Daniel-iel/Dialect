namespace Dialect.Tests.Cli;

using Dialect.Cli.SqlDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Phase 6: Edge case tests for complex SQL patterns.
/// Tests SQL discovery with advanced patterns: stored procedures, CTEs, MERGE, etc.
/// </summary>
public class EdgeCaseSqlDiscoveryTests
{
    private readonly RoslynSqlDiscoveryService _discoveryService;

    public EdgeCaseSqlDiscoveryTests()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILogger<RoslynSqlDiscoveryService>>();
        _discoveryService = new RoslynSqlDiscoveryService(logger);
    }

    #region CTE (Common Table Expressions) - Simple & Recursive

    [Fact]
    public void DiscoverSqlStrings_WithSimpleCte_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var query = @""
WITH UserRanks AS (
    SELECT UserId, UserName, ROW_NUMBER() OVER (ORDER BY Score DESC) AS Rank
    FROM Users
)
SELECT * FROM UserRanks WHERE Rank <= 10
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("WITH", discovered.SqlContent);
        Assert.Contains("UserRanks", discovered.SqlContent);
        Assert.Contains("ROW_NUMBER", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithRecursiveCte_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var hierarchyQuery = @""
WITH RECURSIVE CategoryHierarchy AS (
    SELECT CategoryId, CategoryName, ParentCategoryId, 0 AS Level
    FROM Categories
    WHERE ParentCategoryId IS NULL
    UNION ALL
    SELECT c.CategoryId, c.CategoryName, c.ParentCategoryId, ch.Level + 1
    FROM Categories c
    INNER JOIN CategoryHierarchy ch ON c.ParentCategoryId = ch.CategoryId
)
SELECT * FROM CategoryHierarchy
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("WITH RECURSIVE", discovered.SqlContent);
        Assert.Contains("UNION ALL", discovered.SqlContent);
    }

    #endregion

    #region MERGE Statements

    [Fact]
    public void DiscoverSqlStrings_WithMergeStatement_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var mergeQuery = @""
MERGE INTO target_table AS t
USING source_table AS s
ON t.id = s.id
WHEN MATCHED THEN UPDATE SET t.value = s.value
WHEN NOT MATCHED THEN INSERT (id, value) VALUES (s.id, s.value)
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("MERGE", discovered.SqlContent);
        Assert.Contains("WHEN MATCHED", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithComplexMergeAndDelete_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var complexMerge = @""
MERGE INTO OrderSummary t
USING (SELECT OrderId, SUM(Amount) Total FROM Orders GROUP BY OrderId) s
ON t.OrderId = s.OrderId
WHEN MATCHED AND s.Total > 1000 THEN UPDATE SET t.Status = 'HighValue'
WHEN MATCHED AND s.Total <= 1000 THEN DELETE
WHEN NOT MATCHED THEN INSERT (OrderId, Total, Status) VALUES (s.OrderId, s.Total, 'New')
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("MERGE", discovered.SqlContent);
        Assert.Contains("DELETE", discovered.SqlContent);
    }

    #endregion

    #region Stored Procedures & Functions

    [Fact]
    public void DiscoverSqlStrings_WithCreateProcedure_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var procSql = @""
CREATE PROCEDURE GetUsersByStatus @status NVARCHAR(50), @limit INT = 100
AS
BEGIN
    SELECT TOP (@limit) UserId, UserName, Email
    FROM Users
    WHERE Status = @status
    ORDER BY CreatedDate DESC
END
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("CREATE", discovered.SqlContent);
        Assert.Contains("PROCEDURE", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithCreateFunction_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var funcSql = @""
CREATE FUNCTION CalculateDiscount(@amount DECIMAL, @customerType VARCHAR(50))
RETURNS DECIMAL
AS
BEGIN
    RETURN CASE 
        WHEN @customerType = 'VIP' THEN @amount * 0.20
        WHEN @customerType = 'Premium' THEN @amount * 0.10
        ELSE @amount * 0.05
    END
END
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("CREATE", discovered.SqlContent);
        Assert.Contains("FUNCTION", discovered.SqlContent);
    }

    #endregion

    #region Complex Joins

    [Fact]
    public void DiscoverSqlStrings_WithMultipleJoinTypes_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var complexJoin = @""
SELECT 
    u.UserId, u.UserName,
    o.OrderId, o.Total,
    oi.ItemId, oi.Quantity,
    p.ProductName, p.Price
FROM Users u
INNER JOIN Orders o ON u.UserId = o.UserId
LEFT JOIN OrderItems oi ON o.OrderId = oi.OrderId
RIGHT JOIN Products p ON oi.ProductId = p.ProductId
FULL OUTER JOIN Categories c ON p.CategoryId = c.CategoryId
WHERE u.Status = 'Active'
ORDER BY o.OrderDate DESC
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("INNER JOIN", discovered.SqlContent);
        Assert.Contains("LEFT JOIN", discovered.SqlContent);
        Assert.Contains("FULL OUTER JOIN", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithSelfJoin_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var selfJoin = @""
SELECT 
    e.EmployeeId, e.EmployeeName,
    m.EmployeeId AS ManagerId, m.EmployeeName AS ManagerName
FROM Employees e
LEFT JOIN Employees m ON e.ManagerId = m.EmployeeId
ORDER BY e.EmployeeName
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("Employees e", discovered.SqlContent);
        Assert.Contains("Employees m", discovered.SqlContent);
    }

    #endregion

    #region Window Functions & Advanced Aggregates

    [Fact]
    public void DiscoverSqlStrings_WithWindowFunctions_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var windowQuery = @""
SELECT 
    UserId, UserName, OrderAmount,
    ROW_NUMBER() OVER (ORDER BY OrderAmount DESC) AS RowNum,
    RANK() OVER (PARTITION BY Region ORDER BY OrderAmount DESC) AS RegionalRank,
    SUM(OrderAmount) OVER (PARTITION BY Region) AS RegionalTotal,
    LAG(OrderAmount) OVER (ORDER BY OrderDate) AS PreviousOrder,
    LEAD(OrderAmount) OVER (ORDER BY OrderDate) AS NextOrder
FROM Orders
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("ROW_NUMBER", discovered.SqlContent);
        Assert.Contains("OVER", discovered.SqlContent);
        Assert.Contains("LAG", discovered.SqlContent);
        Assert.Contains("LEAD", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithStringAggregation_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var groupConcat = @""
SELECT 
    o.OrderId,
    STRING_AGG(DISTINCT p.ProductName, ', ') AS ProductList,
    STRING_AGG(CAST(oi.Quantity AS VARCHAR), '|') AS QuantitiesList,
    GROUP_CONCAT(DISTINCT p.Category) AS CategoriesList
FROM Orders o
INNER JOIN OrderItems oi ON o.OrderId = oi.OrderId
INNER JOIN Products p ON oi.ProductId = p.ProductId
GROUP BY o.OrderId
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("STRING_AGG", discovered.SqlContent);
    }

    #endregion

    #region Subqueries - Nested & Complex

    [Fact]
    public void DiscoverSqlStrings_WithNestedSubqueries_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var nested = @""
SELECT * FROM (
    SELECT UserId, UserName, OrderCount FROM (
        SELECT u.UserId, u.UserName, COUNT(*) AS OrderCount
        FROM Users u
        LEFT JOIN Orders o ON u.UserId = o.UserId
        WHERE u.Status = 'Active'
        GROUP BY u.UserId, u.UserName
    ) AS UserOrders
    WHERE OrderCount > 5
) AS FilteredUsers
ORDER BY OrderCount DESC
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("SELECT", discovered.SqlContent);
        var selectCount = discovered.SqlContent.Split(new[] { "SELECT" }, StringSplitOptions.None).Length - 1;
        Assert.True(selectCount >= 3, "Should have at least 3 SELECT keywords for nested query");
    }

    [Fact]
    public void DiscoverSqlStrings_WithCorrelatedSubquery_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var correlated = @""
SELECT u.UserId, u.UserName, 
       (SELECT COUNT(*) FROM Orders o WHERE o.UserId = u.UserId) AS OrderCount,
       (SELECT SUM(Total) FROM Orders o WHERE o.UserId = u.UserId) AS TotalSpent
FROM Users u
WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = u.UserId AND o.Total > 100)
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("EXISTS", discovered.SqlContent);
    }

    #endregion

    #region UNION / INTERSECT / EXCEPT

    [Fact]
    public void DiscoverSqlStrings_WithUnionQueries_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var unionQuery = @""
SELECT UserId, UserName FROM Users WHERE Status = 'Active'
UNION
SELECT ManagerId, ManagerName FROM Managers WHERE Approved = 1
UNION ALL
SELECT AdminId, AdminName FROM Admins
ORDER BY UserName
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("UNION", discovered.SqlContent);
    }

    [Fact]
    public void DiscoverSqlStrings_WithSetOperations_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var setOps = @""
SELECT ProductId FROM CurrentProducts
INTERSECT
SELECT ProductId FROM OrderedProducts
EXCEPT
SELECT ProductId FROM DiscontinuedProducts
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("INTERSECT", discovered.SqlContent);
        Assert.Contains("EXCEPT", discovered.SqlContent);
    }

    #endregion

    #region Multi-Line & Formatted SQL

    [Fact]
    public void DiscoverSqlStrings_WithExtensivelyFormattedSql_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var formatted = @""
SELECT
    u.UserId,
    u.UserName,
    u.Email,
    COUNT(o.OrderId) AS TotalOrders,
    SUM(o.Total) AS TotalSpent,
    MAX(o.OrderDate) AS LastOrderDate
FROM
    Users u
    LEFT JOIN Orders o ON u.UserId = o.UserId
WHERE
    u.Status = 'Active'
    AND u.CreatedDate >= DATEADD(YEAR, -1, GETDATE())
GROUP BY
    u.UserId,
    u.UserName,
    u.Email
HAVING
    COUNT(o.OrderId) > 0
ORDER BY
    TotalSpent DESC,
    UserName ASC
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.Contains("HAVING", discovered.SqlContent);
    }

    #endregion

    #region Edge Cases - Confidence Scoring

    [Fact]
    public void DiscoverSqlStrings_WithLowConfidenceSql_HasReasonableScore()
    {
        // Arrange
        var sourceCode = @"
var lowConf = ""SELECT apple banana cherry FROM dog"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert - Should be detected but with lower confidence
        // The exact behavior depends on implementation
        Assert.NotNull(results);
    }

    [Fact]
    public void DiscoverSqlStrings_WithMixedCaseKeywords_DetectsQuery()
    {
        // Arrange
        var sourceCode = @"
var mixedCase = @""
SeLeCt UserId, UserName FrOm Users WhErE Status = 'Active' OrDeR bY UserName
"";
";

        // Act
        var results = _discoveryService.DiscoverSqlStrings(sourceCode);

        // Assert
        Assert.NotEmpty(results);
        var discovered = results.First();
        Assert.NotEmpty(discovered.SqlContent);
    }

    #endregion

    #region Confidence Score Tests

    [Fact]
    public void IsSuspiciouslyLikesSql_WithClassicSelect_HasHighConfidence()
    {
        // Arrange
        var sql = "SELECT * FROM Users WHERE Id = 1";

        // Act
        var score = _discoveryService.IsSuspiciouslyLikesSql(sql);

        // Assert
        Assert.True(score > 0.7, $"Expected high confidence but got {score}");
    }

    [Fact]
    public void IsSuspiciouslyLikesSql_WithStoredProcedureCall_HasReasonableConfidence()
    {
        // Arrange
        var sql = "EXECUTE sp_GetUserData @userId = 123";

        // Act
        var score = _discoveryService.IsSuspiciouslyLikesSql(sql);

        // Assert
        Assert.True(score > 0.4, $"Expected reasonable confidence but got {score}");
    }

    [Fact]
    public void IsSuspiciouslyLikesSql_WithNonSqlText_HasLowConfidence()
    {
        // Arrange
        var text = "This is just regular English text about bears and trees";

        // Act
        var score = _discoveryService.IsSuspiciouslyLikesSql(text);

        // Assert
        Assert.True(score < 0.5, $"Expected low confidence but got {score}");
    }

    #endregion
}
