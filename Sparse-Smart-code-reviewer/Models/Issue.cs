namespace Sparse_Smart_code_reviewer.Models
{
    public class Issue
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int? LineNumber { get; set; }

        public string Suggestion { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Review Review { get; set; } = null!;

    }

}
