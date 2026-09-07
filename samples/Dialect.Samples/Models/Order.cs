namespace Dialect.Samples.Models;

/// <summary>
/// Order model representing an order in the database.
/// Maps to the orders table (snake_case) across SQL Server, PostgreSQL, and MySQL.
/// Column names are automatically mapped via SnakeCaseTypeMap in DapperExecutor.
/// </summary>
public class Order
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
}
