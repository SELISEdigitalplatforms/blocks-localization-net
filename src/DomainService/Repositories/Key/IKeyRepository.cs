using DomainService.Services;

namespace DomainService.Repositories
{
    public interface IKeyRepository
    {
        Task SaveKeyAsync(BlocksLanguageKey key);
        Task<BlocksLanguageKey> GetKeyByNameAsync(string KeyName, string moduleId);
        Task<GetKeysQueryResponse> GetAllKeysAsync(GetKeysRequest query);
        Task<List<Key>> GetAllKeysByModuleAsync(string moduleId);
        Task<bool> SaveNewUilmFiles(List<UilmFile> uilmfiles);
        Task<long> DeleteOldUilmFiles(List<UilmFile> uilmfiles);
        Task<UilmFile> GetUilmFile(GetUilmFileRequest request);
    }
}
