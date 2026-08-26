using Sparse_Smart_code_reviewer.DTOs.Code;

namespace Sparse_Smart_code_reviewer.Services.interfaces
{
    public interface ICodeService
    {
        Task<Models.Code> CreateAsync(CreateCodeDTO dto, int userId);

    }
}
