using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sparse_Smart_code_reviewer.Data;
using Sparse_Smart_code_reviewer.DTOs.Auth;
using Sparse_Smart_code_reviewer.External.Github;
using Sparse_Smart_code_reviewer.Models;
using Sparse_Smart_code_reviewer.Services.interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
namespace Sparse_Smart_code_reviewer.Services.services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<User> RegisterAsync(RegisterDTO dto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (existingUser != null)
            throw new Exception("Email already exists.");

        var user = new User
        {
            Name = dto.UserName,
            Email = dto.Email
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            dto.Password
        );

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<string> LoginAsync(LoginDTO dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
            throw new Exception("Invalid email or password.");

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password
        );

        if (result == PasswordVerificationResult.Failed)
            throw new Exception("Invalid email or password.");

        return GenerateJwtToken(user);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        var key = jwtSettings["Key"]
            ?? throw new InvalidOperationException("JWT Key is missing.");

        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),

            new Claim(
                ClaimTypes.Name,
                user.Name
            ),

            new Claim(
                ClaimTypes.Email,
                user.Email
            )
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key)
        );

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(jwtSettings["ExpiresInMinutes"] ?? "60")
            ),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<AuthResponseDto> LoginWithGoogleAsync(string idToken)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[]
                    {
                        _configuration["Authentication:Google:ClientId"]!
                    }
                });
        }
        catch
        {
            throw new UnauthorizedAccessException("Invalid Google token.");
        }

        var email = payload.Email;
        var name = payload.Name;
        var googleId = payload.Subject;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                Email = email,
                Name = name
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();
        }

        var token = GenerateJwtToken(user);

        return new AuthResponseDto
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            Token = token
        };
    }

    public async Task<AuthResponseDto> LoginWithGitHubAsync(string code)
    {
        using var httpClient = new HttpClient();

        // =====================================================
        // 1. Exchange GitHub Code for Access Token
        // =====================================================

        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] =
                _configuration["Authentication:GitHub:ClientId"]!,

            ["client_secret"] =
                _configuration["Authentication:GitHub:ClientSecret"]!,

            ["code"] = code
        };

        using var tokenResponse = await httpClient.PostAsync(
            "https://github.com/login/oauth/access_token",
            new FormUrlEncodedContent(tokenRequest)
        );

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException(
                "Failed to get GitHub access token."
            );
        }

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        var tokenData = JsonSerializer.Deserialize<GitHubTokenResponse>(
            tokenContent
        );

        if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
        {
            throw new UnauthorizedAccessException(
                "Invalid GitHub access token."
            );
        }

        // =====================================================
        // 2. Get GitHub User
        // =====================================================

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenData.AccessToken
            );

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Sparse");

        var userResponse = await httpClient.GetAsync(
            "https://api.github.com/user"
        );

        if (!userResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException(
                "Failed to get GitHub user."
            );
        }

        var userContent =
            await userResponse.Content.ReadAsStringAsync();

        var githubUser =
            JsonSerializer.Deserialize<GitHubUserResponse>(
                userContent
            );

        if (githubUser == null)
        {
            throw new UnauthorizedAccessException(
                "Invalid GitHub user."
            );
        }

        // =====================================================
        // 3. Get GitHub Email
        // =====================================================

        var emailResponse = await httpClient.GetAsync(
            "https://api.github.com/user/emails"
        );

        if (!emailResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException(
                "Failed to get GitHub email."
            );
        }

        var emailContent =
            await emailResponse.Content.ReadAsStringAsync();

        var emails =
            JsonSerializer.Deserialize<List<GitHubEmailResponse>>(
                emailContent
            );

        var primaryEmail = emails?
            .FirstOrDefault(x => x.Primary && x.Verified)
            ?.Email;

        if (string.IsNullOrEmpty(primaryEmail))
        {
            throw new UnauthorizedAccessException(
                "No verified GitHub email found."
            );
        }

        // =====================================================
        // 4. Find User in Database
        // =====================================================

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == primaryEmail);

        // =====================================================
        // 5. Create User if not exists
        // =====================================================

        if (user == null)
        {
            user = new User
            {
                Name = githubUser.Name ?? githubUser.Login,
                Email = primaryEmail
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();
        }

        // =====================================================
        // 6. Generate Sparse JWT
        // =====================================================

        var jwtToken = GenerateJwtToken(user);

        return new AuthResponseDto
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            Token = jwtToken
        };
    }
}