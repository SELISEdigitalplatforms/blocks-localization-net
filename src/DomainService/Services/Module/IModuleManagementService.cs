using DomainService.Shared;

namespace DomainService.Services
{
    public interface IModuleManagementService
    {
        Task<ApiResponse> SaveModuleAsync(SaveModuleRequest module);
        Task<List<Module>> GetModulesAsync();
    }
}
