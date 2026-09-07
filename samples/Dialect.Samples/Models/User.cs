namespace Dialect.Samples.Models;

/// <summary>
/// User model representing a user in the database.
/// Maps to the users table (snake_case) across SQL Server, PostgreSQL, and MySQL.
/// Column names are automatically mapped via SnakeCaseTypeMap in DapperExecutor.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
