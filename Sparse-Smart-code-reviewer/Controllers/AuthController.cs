using Microsoft.AspNetCore.Mvc;
using Sparse_Smart_code_reviewer.DTOs.Auth;
using Sparse_Smart_code_reviewer.Services.interfaces;

namespace Sparse_Smart_code_reviewer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    [HttpPost("github")]
    public async Task<IActionResult> GitHubLogin(
     [FromBody] GitHubLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(new
            {
                message = "GitHub authorization code is required."
            });
        }

        try
        {
            var result = await _authService
                .LoginWithGitHubAsync(dto.Code);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
    }
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(
       [FromBody] GoogleLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest(new
            {
                message = "Google ID token is required."
            });
        }

        try
        {
            var result = await _authService
                .LoginWithGoogleAsync(dto.IdToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO dto)
    {
        var user = await _authService.RegisterAsync(dto);

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        var token = await _authService.LoginAsync(dto);

        return Ok(new
        {
            token
        });
    }
}