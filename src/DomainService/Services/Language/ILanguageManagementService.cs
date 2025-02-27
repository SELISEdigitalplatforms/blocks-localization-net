using Blocks.Genesis;
using DomainService.Shared;

namespace DomainService.Services
{
    public interface ILanguageManagementService
    {
        Task<ApiResponse> SaveLanguageAsync(Language language);
        Task<List<Language>> GetLanguagesAsync();
        Task<BaseMutationResponse> DeleteAsysnc(DeleteLanguageRequest request);
        Task<BaseMutationResponse> SetDefaultLanguage(SetDefaultLanguageRequest request);
    }
}
