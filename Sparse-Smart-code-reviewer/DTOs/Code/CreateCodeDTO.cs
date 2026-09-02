using System.ComponentModel.DataAnnotations;

namespace Sparse_Smart_code_reviewer.DTOs.Code
{
    public class CreateCodeDTO
    {
        [Required(ErrorMessage = "Code content is required.")]
        [MaxLength(10_000_000, ErrorMessage = "Code content cannot exceed 10 MB.")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Language is required.")]
        [MaxLength(50, ErrorMessage = "Language cannot exceed 50 characters.")]
        public string Language { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "File name cannot exceed 255 characters.")]
        public string? FileName { get; set; }
    }
}
