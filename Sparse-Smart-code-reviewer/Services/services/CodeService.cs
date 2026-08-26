using Sparse_Smart_code_reviewer.Data;
using Sparse_Smart_code_reviewer.DTOs.Code;
using Sparse_Smart_code_reviewer.Models;
using Sparse_Smart_code_reviewer.Services.interfaces;

namespace Sparse_Smart_code_reviewer.Services.services
{
    public class CodeService : ICodeService
    {
        private readonly AppDbContext _context;

        public CodeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Code> CreateAsync(CreateCodeDTO dto, int userId)
        {
            var code = new Code
            {
                Content = dto.Content,
                Language = dto.Language,
                FileName = dto.FileName,
                UserId = userId
            };

            _context.Codes.Add(code);

            await _context.SaveChangesAsync();

            return code;
        }
    }
}
