using DomainService.Services;
using DomainService.Shared.Entities;
using MongoDB.Driver;

namespace DomainService.Repositories
{
    public interface IEnvironmentDataMigrationRepository
    {
        Task<List<BlocksLanguageModule>> GetAllModulesAsync(string tenantId);
        Task<List<BlocksLanguageKey>> GetAllKeysAsync(string tenantId);
        Task BulkUpsertModulesAsync(List<BlocksLanguageModule> modules, string tenantId, bool shouldOverwrite);
        Task BulkUpsertKeysAsync(List<BlocksLanguageKey> keys, string tenantId, bool shouldOverwrite);
        Task UpdateMigrationTrackerAsync(string trackerId, ServiceMigrationStatus LanguageServiceStatus);
    }
}
