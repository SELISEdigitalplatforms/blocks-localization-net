using DomainService.Services;
using DomainService.Shared.Entities;
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
        Task<T> GetUilmResourceKey<T>(Expression<Func<BlocksLanguageKey, bool>> expression);
        Task<BlocksLanguageKey> GetUilmResourceKey(Expression<Func<BlocksLanguageKey, bool>> expression, string tenantId);
        Task InsertUilmResourceKeys(IEnumerable<BlocksLanguageKey> entities);
        Task InsertUilmResourceKeys(IEnumerable<BlocksLanguageKey> entities, string tenantId);
        Task UpdateBulkUilmApplications(List<BlocksLanguageModule> uilmApplicationsToBeUpdated, string organizationId, bool isExternal, string clientTenantId);
        Task<bool> UpdateKeysCountOfAppAsync(string appId, bool isExternal, string tenantId, string organizationId);    
        Task InsertUilmApplications(List<BlocksLanguageModule> uilmApplicationsToBeInserted, string clientTenantId);
        Task InsertUilmApplications(IEnumerable<BlocksLanguageModule> entities);
        Task<List<T>> GetUilmApplications<T>(Expression<Func<BlocksLanguageModule, bool>> expression, string clientTenantId);
        Task<List<BlocksLanguageKey>> GetUilmResourceKeys(Expression<Func<BlocksLanguageKey, bool>> expression, string tenantId);
        Task<List<T>> GetUilmResourceKeys<T>(Expression<Func<BlocksLanguageKey, bool>> expression);
        Task<BlocksLanguage> GetLanguageSettingAsync(string clientTenantId);
        Task<List<BlocksLanguage>> GetAllLanguagesAsync(string clientTenantId);
    }
}
