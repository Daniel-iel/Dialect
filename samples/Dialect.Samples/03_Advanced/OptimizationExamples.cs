namespace Dialect.Samples._03_Advanced;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Core.Query;
using Dialect.SqlServer.Query;
using Dialect.Samples.Utilities;

/// <summary>
/// Query optimization and analysis examples.
/// 
/// NOTE: This is an educational example demonstrating the framework's query analysis capabilities.
/// The optimization analysis (query parsing, predicate analysis, join analysis) demonstrates 
/// how the Dialect framework can parse and analyze SQL queries for optimization opportunities.
/// 
/// In a real-world scenario, these would integrate with actual SQL execution plans and
/// performance metrics from live database instances.
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

    private void QueryParsing()
    {
        OutputFormatter.PrintSubHeader("Example 1: Query Parsing and Analysis");

        // Build and compile a query for analysis
        var compiledResults = CompileForAllDialects(dialect =>
        {
            var joinCondition = new ComparisonNode(
                new Column("o.UserId"),
                ComparisonOperator.Equal,
                new Column("u.UserId")
            );
            
            return SqlBuilder.Select("OrderId", "Username", "Total")
                .From("Orders o")
                .InnerJoin("Users u", joinCondition)
                .OrderBy("Total DESC")
                .Build()
                .Compile(dialect);
        });


        AddScenario("Query Parsing - JOIN with WHERE and ORDER BY", compiledResults);

        const string sql = "SELECT o.OrderId, u.Username, o.Total FROM Orders o JOIN Users u ON o.UserId = u.UserId WHERE o.Total > 500 ORDER BY o.Total DESC";

        var parser = new SqlServerQueryParser();
        var analysis = parser.Parse(sql);

        var queryAnalysisText = @$"
  Analysis Results:
    - Has WHERE clause: {analysis.WhereClauses.Length > 0}
    - Has JOIN: {analysis.JoinClauses.Length > 0}
    - Number of JOINs: {analysis.JoinClauses.Length}
    - Has ORDER BY: {analysis.OrderByClauses.Length > 0}
    - Has GROUP BY: {analysis.GroupByClauses.Length > 0}";

        Console.WriteLine(queryAnalysisText);
    }

    private void PredicateAnalysis()
    {
        OutputFormatter.PrintSubHeader("Example 2: Predicate Analysis");

        // Build and compile a query with multiple predicates
        var compiledResults = CompileForAllDialects(dialect =>
            SqlBuilder.Select("OrderId", "UserId", "Total", "OrderDate")
                .From("Orders")
                .Build()
                .Compile(dialect)
        );

        AddScenario("Predicate Analysis - Multi-condition WHERE", compiledResults);

        const string sql = "SELECT * FROM Orders WHERE UserId = 1 AND Total > 100 AND OrderDate >= '2024-01-01'";

        var analyzer = new PredicateAnalyzer();

        // Analyze individual predicates from the WHERE clause
        var predicate1 = analyzer.Analyze("UserId = 1");
        var predicate2 = analyzer.Analyze("Total > 100");
        var predicate3 = analyzer.Analyze("OrderDate >= '2024-01-01'");

        var predicateAnalysisText = @$"
  Predicate Analysis:
    - Total predicates: 3
    - Predicate 1: {predicate1.Column} ({predicate1.OperatorType}) - Selectivity: {predicate1.Selectivity:P1}, Indexable: {predicate1.IsIndexable}
    - Predicate 2: {predicate2.Column} ({predicate2.OperatorType}) - Selectivity: {predicate2.Selectivity:P1}, Indexable: {predicate2.IsIndexable}
    - Predicate 3: {predicate3.Column} ({predicate3.OperatorType}) - Selectivity: {predicate3.Selectivity:P1}, Indexable: {predicate3.IsIndexable}
    - Indexed columns recommended: UserId, OrderDate";

        Console.WriteLine(predicateAnalysisText);
    }

    private void JoinAnalysis()
    {
        OutputFormatter.PrintSubHeader("Example 3: JOIN Analysis");

        // Build and compile a query with multiple JOINs
        var compiledResults = CompileForAllDialects(dialect =>
        {
            var join1 = new ComparisonNode(
                new Column("o.UserId"),
                ComparisonOperator.Equal,
                new Column("u.UserId")
            );
            var join2 = new ComparisonNode(
                new Column("o.OrderId"),
                ComparisonOperator.Equal,
                new Column("oi.OrderId")
            );
            var join3 = new ComparisonNode(
                new Column("oi.ProductId"),
                ComparisonOperator.Equal,
                new Column("p.ProductId")
            );
            
            return SqlBuilder.Select("OrderId", "UserId", "Total")
                .From("Orders o")
                .InnerJoin("Users u", join1)
                .InnerJoin("OrderItems oi", join2)
                .InnerJoin("Products p", join3)
                .Build()
                .Compile(dialect);
        });


        AddScenario("JOIN Analysis - Multi-table JOIN with WHERE", compiledResults);

        const string sql = "SELECT * FROM Orders o JOIN Users u ON o.UserId = u.UserId JOIN OrderItems oi ON o.OrderId = oi.OrderId JOIN Products p ON oi.ProductId = p.ProductId WHERE o.Total > 500";

        var analyzer = new JoinAnalyzer();

        // Analyze individual JOIN clauses
        var join1 = analyzer.Analyze("Orders o JOIN Users u ON o.UserId = u.UserId");
        var join2 = analyzer.Analyze("Orders o JOIN OrderItems oi ON o.OrderId = oi.OrderId");
        var join3 = analyzer.Analyze("OrderItems oi JOIN Products p ON oi.ProductId = p.ProductId");

        var joinAnalysisText = @$"
  JOIN Analysis:
    - Total joins: 3
    - Join 1: {join1.JoinType} between {join1.LeftTable} and {join1.RightTable} - Optimal: {join1.IsOptimal}
    - Join 2: {join2.JoinType} between {join2.LeftTable} and {join2.RightTable} - Optimal: {join2.IsOptimal}
    - Join 3: {join3.JoinType} between {join3.LeftTable} and {join3.RightTable} - Optimal: {join3.IsOptimal}
    - Recommendation: Consider materializing intermediate results in CTEs for complex joins";

        Console.WriteLine(joinAnalysisText);
    }
}
