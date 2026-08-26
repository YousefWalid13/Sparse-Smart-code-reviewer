namespace Sparse_Smart_code_reviewer.Models
{
    public class Code
    {
        public int Id { get; set; }

        public string Content { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string? FileName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        // Navigation Property
        public User User { get; set; } = null!;
        public ICollection<Issue> Issues { get; set; } = new List<Issue>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
