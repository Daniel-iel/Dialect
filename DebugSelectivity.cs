using System.Text.Json;
using Dialect.SqlServer.Performance;

var analyzer = new SqlServerPlanAnalyzer();

var planJson = """
{
    "Root": {
        "RelOp": "TableScan",
        "Object": {"Table": "Users"},
        "EstimatedRows": 50,
        "EstimatedTotalSubtreeCost": "10.0"
    },
    "EstimatedTotalSubtreeCost": "10.0",
    "EstimatedRows": 50
}
""";

var queryText = "SELECT * FROM Users WHERE Status = 'Active'";

var metrics = analyzer.AnalyzePlan(planJson, queryText);

Console.WriteLine($"TotalRowsProduced: {metrics.TotalRowsProduced}");
Console.WriteLine($"TableScanCount: {metrics.TableScanCount}");
Console.WriteLine($"Selectivity: {metrics.Selectivity}");
