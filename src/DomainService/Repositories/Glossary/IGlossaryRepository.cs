using DomainService.Services;

namespace DomainService.Repositories
{
    public interface IGlossaryRepository
    {
        Task<GetGlossariesResponse> GetAllAsync(GetGlossariesRequest request);
        Task<Glossary> GetByIdAsync(string itemId);
        Task SaveAsync(BlocksGlossary glossary);
        Task DeleteAsync(string itemId);
    }
}
