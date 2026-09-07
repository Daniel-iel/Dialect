namespace Dialect.Samples.Models;

/// <summary>
/// OrderItem model representing a line item in an order.
/// Maps to the order_items table (snake_case) across SQL Server, PostgreSQL, and MySQL.
/// Column names are automatically mapped via SnakeCaseTypeMap in DapperExecutor.
/// </summary>
public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
