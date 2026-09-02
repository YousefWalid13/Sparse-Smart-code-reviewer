using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Sparse_Smart_code_reviewer.DTOs.Code;
using Sparse_Smart_code_reviewer.Services.interfaces;

namespace Sparse_Smart_code_reviewer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CodeController : ControllerBase
{
    private readonly ICodeService _codeService;

    public CodeController(ICodeService codeService)
    {
        _codeService = codeService;
    }


    // ============================================================
    // GET CURRENT USER ID
    // ============================================================

    private int GetUserId()
    {
        // The user ID comes from the authenticated JWT.
        //
        // We NEVER accept UserId from the client.
        //
        // This prevents a user from submitting code on behalf
        // of another user simply by changing a UserId field.

        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(userIdClaim, out var userId))
        {
            // This should normally never happen because the endpoint
            // requires authentication.
            //
            // However, we should not blindly use int.Parse or the
            // null-forgiving operator (!) for security-sensitive data.
            throw new UnauthorizedAccessException(
                "Invalid user identity.");
        }

        return userId;
    }


    // ============================================================
    // PASTE CODE
    // ============================================================

    [HttpPost("paste")]
    public async Task<IActionResult> PasteCode(
        [FromBody] CreateCodeDTO dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var code = await _codeService.CreateAsync(
            dto,
            userId,
            cancellationToken);

        return Ok(code);
    }


    // ============================================================
    // UPLOAD SINGLE FILE
    // ============================================================

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _codeService.CreateFromFileAsync(
            file,
            userId,
            cancellationToken);

        return Ok(result);
    }


    // ============================================================
    // UPLOAD ZIP
    // ============================================================

    [HttpPost("zip")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadZip(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _codeService.CreateFromZipAsync(
            file,
            userId,
            cancellationToken);

        return Ok(result);
    }


    // ============================================================
    // GIT REPOSITORY
    // ============================================================

    [HttpPost("git")]
    public async Task<IActionResult> GitRepository(
        [FromQuery] string repositoryUrl,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _codeService.CreateFromGitAsync(
            repositoryUrl,
            userId,
            cancellationToken);

        return Ok(result);
    }


    // ============================================================
    // GITHUB REPOSITORY
    // ============================================================

    [HttpPost("github")]
    public async Task<IActionResult> GitHubRepository(
        [FromQuery] string repositoryUrl,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _codeService.CreateFromGitHubAsync(
            repositoryUrl,
            userId,
            cancellationToken);

        return Ok(result);
    }
}