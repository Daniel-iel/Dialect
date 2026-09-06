namespace TestApp;

public class UserRepository
{
    public void GetUser(int id)
    {
        string sql = "SELECT * FROM users WHERE id = @id";
        // Execute SQL
    }

    public void ListUsers()
    {
        string query = "SELECT id, name, email FROM users ORDER BY name";
        // Execute
    }

    public void DeleteUser(int userId)
    {
        var cmd = "DELETE FROM users WHERE id = @userId AND status != 'active'";
        // Execute
    }
}
