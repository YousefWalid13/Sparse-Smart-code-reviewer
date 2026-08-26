using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCodeDTO dto)
    {
        var userId = 1; // temporary

        var code = await _codeService.CreateAsync(dto, userId);

        return Ok(code);
    }
}