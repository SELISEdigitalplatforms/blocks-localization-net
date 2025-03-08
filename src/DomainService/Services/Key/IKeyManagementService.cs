using Blocks.Genesis;
using DomainService.Shared;
using DomainService.Shared.Events;

namespace DomainService.Services
{
    public interface IKeyManagementService
    {
        Task<ApiResponse> SaveKeyAsync(Key key);
        Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query);
        Task<bool> GenerateAsync(GenerateUilmFilesEvent command);
        Task<string> GetUilmFile(GetUilmFileRequest request);
        Task<Key?> GetAsync(GetKeyRequest request);
        Task<BaseMutationResponse> DeleteAsysnc(DeleteKeyRequest request);
        Task SendTranslateAllEvent(TranslateAllRequest request);
        Task SendGenerateUilmFilesEvent(GenerateUilmFilesRequest request);
        Task<bool> ChangeAll(TranslateAllEvent request);
    }
}
