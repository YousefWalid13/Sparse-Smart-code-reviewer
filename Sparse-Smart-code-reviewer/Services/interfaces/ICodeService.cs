using Sparse_Smart_code_reviewer.DTOs.Code;
using Sparse_Smart_code_reviewer.Models;

namespace Sparse_Smart_code_reviewer.Services.interfaces
{
    public interface ICodeService
    {
        // Paste code directly from the user.
        Task<Code> CreateAsync(
            CreateCodeDTO dto,
            int userId,
            CancellationToken cancellationToken = default);

        // Upload and process a single source-code file.
        Task<Code> CreateFromFileAsync(
            IFormFile file,
            int userId,
            CancellationToken cancellationToken = default);

        // Upload, safely extract, validate, and process a ZIP file.
        Task<List<Code>> CreateFromZipAsync(
            IFormFile file,
            int userId,
            CancellationToken cancellationToken = default);

        // Clone and process a Git repository.
        Task<List<Code>> CreateFromGitAsync(
            string repositoryUrl,
            int userId,
            CancellationToken cancellationToken = default);

        // Download and process a GitHub repository.
        Task<List<Code>> CreateFromGitHubAsync(
            string repositoryUrl,
            int userId,
            CancellationToken cancellationToken = default);
    }
}