namespace Sparse_Smart_code_reviewer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserLogin> Logins { get; set; }
        public ICollection<Code> Codes { get; set; } = new List<Code>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
