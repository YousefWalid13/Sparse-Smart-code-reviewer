namespace Sparse_Smart_code_reviewer.DTOs.Code
{
    public class CreateCodeDTO
    {
        public string Content { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string? FileName { get; set; }
    }
}
