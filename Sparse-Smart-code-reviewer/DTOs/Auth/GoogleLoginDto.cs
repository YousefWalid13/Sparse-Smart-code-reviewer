using System.ComponentModel.DataAnnotations;

namespace Sparse_Smart_code_reviewer.DTOs.Auth
{
    public class GoogleLoginDto
    {
        [Required(ErrorMessage = "Google ID token is required.")]
        public string IdToken { get; set; } = string.Empty;
    }
}
