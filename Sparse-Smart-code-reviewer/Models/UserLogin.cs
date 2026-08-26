namespace Sparse_Smart_code_reviewer.Models;

public class UserLogin
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    // Navigation Property
    public User User { get; set; } = null!;
}