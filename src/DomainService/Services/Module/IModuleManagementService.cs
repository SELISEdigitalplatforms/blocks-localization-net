using DomainService.Shared;

namespace DomainService.Services
{
    public interface IModuleManagementService
    {
        Task<ApiResponse> SaveModuleAsync(Module module);
        Task<List<Module>> GetModulesAsync();
    }
}
