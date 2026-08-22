namespace Dialect.Samples._03_Advanced;

using Dialect.Core.AST;
using Dialect.Core.Dialects;
using Dialect.Core.Query;
using Dialect.SqlServer.Query;
using Dialect.Samples.Utilities;

/// <summary>
/// Query optimization and analysis examples.
/// </summary>
public class OptimizationExamples : ExampleBase
{
    public OptimizationExamples() : base(
        "Query Optimization & Analysis",
        "Demonstrates query parsing, predicate analysis, and optimization suggestions")
    {
    }

    public override void Run()
    {
        QueryParsing();
        Console.WriteLine("\n");
        PredicateAnalysis();
        Console.WriteLine("\n");
        JoinAnalysis();
    }

    private static void QueryParsing()
    {
        OutputFormatter.PrintSubHeader("Example 1: Query Parsing and Analysis");
        
        var sql = "SELECT o.OrderId, u.Username, o.Total FROM Orders o JOIN Users u ON o.UserId = u.UserId WHERE o.Total > 500 ORDER BY o.Total DESC";
        
        var parser = new SqlServerQueryParser();
        var analysis = parser.Parse(sql);
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"\n  Analysis Results:");
        Console.WriteLine($"    - Has WHERE clause: {analysis.WhereClauses.Length > 0}");
        Console.WriteLine($"    - Has JOIN: {analysis.JoinClauses.Length > 0}");
        Console.WriteLine($"    - Number of JOINs: {analysis.JoinClauses.Length}");
        Console.WriteLine($"    - Has ORDER BY: {analysis.OrderByClauses.Length > 0}");
        Console.WriteLine($"    - Has GROUP BY: {analysis.GroupByClauses.Length > 0}");
    }

    private static void PredicateAnalysis()
    {
        OutputFormatter.PrintSubHeader("Example 2: Predicate Analysis");
        
        var sql = "SELECT * FROM Orders WHERE UserId = 1 AND Total > 100 AND OrderDate >= '2024-01-01'";
        
        var analyzer = new PredicateAnalyzer();
        
        // Analyze individual predicates from the WHERE clause
        var predicate1 = analyzer.Analyze("UserId = 1");
        var predicate2 = analyzer.Analyze("Total > 100");
        var predicate3 = analyzer.Analyze("OrderDate >= '2024-01-01'");
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"\n  Predicate Analysis:");
        Console.WriteLine($"    - Total predicates: 3");
        Console.WriteLine($"    - Predicate 1: {predicate1.Column} ({predicate1.OperatorType}) - Selectivity: {predicate1.Selectivity:P1}, Indexable: {predicate1.IsIndexable}");
        Console.WriteLine($"    - Predicate 2: {predicate2.Column} ({predicate2.OperatorType}) - Selectivity: {predicate2.Selectivity:P1}, Indexable: {predicate2.IsIndexable}");
        Console.WriteLine($"    - Predicate 3: {predicate3.Column} ({predicate3.OperatorType}) - Selectivity: {predicate3.Selectivity:P1}, Indexable: {predicate3.IsIndexable}");
        Console.WriteLine($"    - Indexed columns recommended: UserId, OrderDate");
    }

    private static void JoinAnalysis()
    {
        OutputFormatter.PrintSubHeader("Example 3: JOIN Analysis");
        
        var sql = "SELECT * FROM Orders o JOIN Users u ON o.UserId = u.UserId JOIN OrderItems oi ON o.OrderId = oi.OrderId JOIN Products p ON oi.ProductId = p.ProductId WHERE o.Total > 500";
        
        var analyzer = new JoinAnalyzer();
        
        // Analyze individual JOIN clauses
        var join1 = analyzer.Analyze("Orders o JOIN Users u ON o.UserId = u.UserId");
        var join2 = analyzer.Analyze("Orders o JOIN OrderItems oi ON o.OrderId = oi.OrderId");
        var join3 = analyzer.Analyze("OrderItems oi JOIN Products p ON oi.ProductId = p.ProductId");
        
        Console.WriteLine($"  Query: {sql}");
        Console.WriteLine($"\n  JOIN Analysis:");
        Console.WriteLine($"    - Total joins: 3");
        Console.WriteLine($"    - Join 1: {join1.JoinType} between {join1.LeftTable} and {join1.RightTable} - Optimal: {join1.IsOptimal}");
        Console.WriteLine($"    - Join 2: {join2.JoinType} between {join2.LeftTable} and {join2.RightTable} - Optimal: {join2.IsOptimal}");
        Console.WriteLine($"    - Join 3: {join3.JoinType} between {join3.LeftTable} and {join3.RightTable} - Optimal: {join3.IsOptimal}");
        Console.WriteLine($"    - Recommendation: Consider materializing intermediate results in CTEs for complex joins");
    }
}
