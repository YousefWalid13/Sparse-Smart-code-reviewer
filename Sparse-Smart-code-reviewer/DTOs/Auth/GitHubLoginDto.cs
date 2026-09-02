using System.ComponentModel.DataAnnotations;

namespace Sparse_Smart_code_reviewer.DTOs.Auth
{
    public class GitHubLoginDto
    {
        [Required(ErrorMessage = "GitHub authorization code is required.")]
        public string Code { get; set; } = string.Empty;
    }
}
