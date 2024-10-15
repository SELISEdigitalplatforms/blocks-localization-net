using DomainService.Services;
namespace DomainService.Repositories
{
    public interface IModuleRepository
    {
        Task SaveAsync(BlocksLanguageModule module);
        Task<BlocksLanguageModule> GetByNameAsync(string name);
        Task<List<Module>> GetAllAsync();
    }
}
