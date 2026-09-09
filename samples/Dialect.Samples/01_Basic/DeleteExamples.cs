namespace Dialect.Samples._01_Basic;

using Dialect.Core.AST;
using Dialect.Core.Compilation;
using Dialect.Core.Dialects;
using Dialect.Core.Fluent;
using Dialect.Samples.Utilities;

/// <summary>
/// Basic DELETE examples demonstrating deletion operations.
/// Uses data-driven scenario definitions for clean, maintainable test organization.
/// </summary>
public class DeleteExamples : ExampleBase
{
    public DeleteExamples() : base(
        "DELETE - Data Removal",
        "Demonstrates DELETE with WHERE clauses and safety features")
    {
    }

    private static readonly ScenarioDefinition[] Scenarios = new[]
    {
        new ScenarioDefinition(
            "DELETE with Simple WHERE",
            "Basic DELETE with simple WHERE condition",
            dialect => SqlBuilder.Delete()
                .From("Users")
                .Where("UserId", 5)
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "DELETE with Parameter",
            "DELETE using parameterized WHERE for SQL injection prevention",
            dialect => SqlBuilder.Delete()
                .From("Orders")
                .Where("OrderId", "@orderId")
                .Build()
                .Compile(dialect)
        ),

        new ScenarioDefinition(
            "DELETE with Multiple Conditions",
            "DELETE with multiple AND conditions in WHERE",
            dialect =>
            {
                var condition1 = new ComparisonNode(new Column("OrderId"), ComparisonOperator.Equal, 1);
                var condition2 = new ComparisonNode(new Column("Quantity"), ComparisonOperator.GreaterThan, 0);
                var combined = new AndNode(condition1, condition2);
                return SqlBuilder.Delete()
                    .From("OrderItems")
                    .Where(combined)
                    .Build()
                    .Compile(dialect);
            }
        ),
    };

    public override void Run()
    {
        foreach (var scenario in Scenarios)
        {
            var results = CompileForAllDialects(scenario.Builder);
            AddScenario(scenario.Name, results);
            Console.WriteLine("\n");
        }
    }
}
