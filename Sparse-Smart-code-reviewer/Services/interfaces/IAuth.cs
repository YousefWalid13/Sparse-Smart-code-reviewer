using Sparse_Smart_code_reviewer.DTOs.Auth;
using Sparse_Smart_code_reviewer.Models;

namespace Sparse_Smart_code_reviewer.Services.interfaces;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterDTO dto);

    Task<string> LoginAsync(LoginDTO dto);
    Task<AuthResponseDto> LoginWithGitHubAsync(string code);
    Task<AuthResponseDto> LoginWithGoogleAsync(string idToken);
}