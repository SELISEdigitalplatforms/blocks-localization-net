using DomainService.Shared;

namespace DomainService.Services
{
    public interface ILanguageManagementService
    {
        Task<ApiResponse> SaveLanguageAsync(Language language);
        Task<List<Language>> GetLanguagesAsync();
    }
}
