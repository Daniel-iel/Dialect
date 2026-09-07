namespace Dialect.Samples.Models;

/// <summary>
/// Product model representing a product in the database.
/// Maps to the products table (snake_case) across SQL Server, PostgreSQL, and MySQL.
/// Column names are automatically mapped via SnakeCaseTypeMap in DapperExecutor.
/// </summary>
public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
