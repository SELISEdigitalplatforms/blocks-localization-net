using DomainService.Services;
using System.Linq.Expressions;

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
        Task<Key> GetByIdAsync(string itemId);
        Task DeleteAsync(string itemId);
        Task<IQueryable<BlocksLanguageKey>> GetUilmResourceKeysWithPage(int page, int size);
        Task<long?> UpdateUilmResourceKeysForChangeAll(List<BlocksLanguageKey> uilmResourceKeys);
    }
}
