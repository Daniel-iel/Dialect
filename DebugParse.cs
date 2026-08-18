using System.Text.Json;

var planJson = """
{
    "Root": {
        "RelOp": "TableScan",
        "Object": {"Table": "Users"},
        "EstimatedRows": 1000,
        "EstimatedTotalSubtreeCost": "5.5"
    },
    "EstimatedTotalSubtreeCost": "5.5",
    "EstimatedRows": 1000,
    "ExecutionTime": 125.5
}
""";

var jsonDoc = JsonDocument.Parse(planJson);
var root = jsonDoc.RootElement;

if (root.TryGetProperty("EstimatedTotalSubtreeCost", out var costProp))
{
    Console.WriteLine($"ValueKind: {costProp.ValueKind}");
    Console.WriteLine($"GetString(): {costProp.GetString()}");
    
    if (costProp.ValueKind == JsonValueKind.String)
    {
        var str = costProp.GetString();
        var parsed = decimal.TryParse(str, out var value) ? value : 0m;
        Console.WriteLine($"Decimal.TryParse result: {parsed}");
    }
}
