namespace Sparse_Smart_code_reviewer.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CodeId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Code Code { get; set; } = null!;
        public User User { get; set; } = null!;
        public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    }
}
