using System.ComponentModel.DataAnnotations;

namespace Sparse_Smart_code_reviewer.DTOs.Auth;


    public class LoginDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
