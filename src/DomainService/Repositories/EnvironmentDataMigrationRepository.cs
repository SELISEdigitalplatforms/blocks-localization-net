using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Entities;
using MongoDB.Driver;

namespace DomainService.Repositories
{
    public class EnvironmentDataMigrationRepository : IEnvironmentDataMigrationRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private const string _modulesCollectionName = "BlocksLanguageModules";
        private const string _keysCollectionName = "BlocksLanguageKeys";

        public EnvironmentDataMigrationRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<List<BlocksLanguageModule>> GetAllModulesAsync(string tenantId)
        {
            var database = _dbContextProvider.GetDatabase(tenantId);
            var collection = database.GetCollection<BlocksLanguageModule>(_modulesCollectionName);
            return await collection.Find(_ => true).ToListAsync();
        }

        public async Task<List<BlocksLanguageKey>> GetAllKeysAsync(string tenantId)
        {
            var database = _dbContextProvider.GetDatabase(tenantId);
            var collection = database.GetCollection<BlocksLanguageKey>(_keysCollectionName);
            return await collection.Find(_ => true).ToListAsync();
        }

        public async Task<List<BlocksLanguageKey>> GetExistingKeysByItemIdsAsync(List<string> itemIds, string tenantId)
        {
            if (!itemIds.Any()) return new List<BlocksLanguageKey>();

            var database = _dbContextProvider.GetDatabase(tenantId);
            var collection = database.GetCollection<BlocksLanguageKey>(_keysCollectionName);
            var filter = Builders<BlocksLanguageKey>.Filter.In(k => k.ItemId, itemIds);
            return await collection.Find(filter).ToListAsync();
        }

        public async Task UpdateMigrationTrackerAsync(string trackerId, ServiceMigrationStatus LanguageServiceStatus)
        {
            var database = _dbContextProvider.GetDatabase();
            var collection = database.GetCollection<MigrationTracker>("MigrationTrackers");

            var filter = Builders<MigrationTracker>.Filter.Eq(t => t.ItemId, trackerId);

            var update = Builders<MigrationTracker>.Update
                .Set(t => t.LastUpdateDate, DateTime.UtcNow)
                .Set(t => t.LanguageService, LanguageServiceStatus);

            await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task BulkUpsertModulesAsync(List<BlocksLanguageModule> modules, string tenantId, bool shouldOverwrite)
        {
            if (!modules.Any()) return;

            var database = _dbContextProvider.GetDatabase(tenantId);
            var collection = database.GetCollection<BlocksLanguageModule>(_modulesCollectionName);

            if (shouldOverwrite)
            {
                // Replace existing + insert new
                var bulkOps = modules.Select(module =>
                {
                    var filter = Builders<BlocksLanguageModule>.Filter.Eq(m => m.ItemId, module.ItemId);
                    return new ReplaceOneModel<BlocksLanguageModule>(filter, module) { IsUpsert = true };
                }).ToList();

                await collection.BulkWriteAsync(bulkOps);
            }
            else
            {
                // Only insert new (use $setOnInsert to avoid overwriting existing data)
                var bulkOps = modules.Select(module =>
                {
                    var filter = Builders<BlocksLanguageModule>.Filter.Eq(m => m.ItemId, module.ItemId);
                    var update = Builders<BlocksLanguageModule>.Update
                        .SetOnInsert(m => m.ItemId, module.ItemId)
                        .SetOnInsert(m => m.ModuleName, module.ModuleName)
                        .SetOnInsert(m => m.Name, module.Name)
                        .SetOnInsert(m => m.CreateDate, module.CreateDate)
                        .SetOnInsert(m => m.LastUpdateDate, module.LastUpdateDate)
                        .SetOnInsert(m => m.TenantId, module.TenantId)
                        .SetOnInsert(m => m.CreatedBy, module.CreatedBy)
                        .SetOnInsert(m => m.LastUpdatedBy, module.LastUpdatedBy);

                    return new UpdateOneModel<BlocksLanguageModule>(filter, update) { IsUpsert = true };
                }).ToList();

                await collection.BulkWriteAsync(bulkOps);
            }
        }

        public async Task<BulkUpsertResult> BulkUpsertKeysAsync(List<BlocksLanguageKey> keys, string tenantId, bool shouldOverwrite)
        {
            var result = new BulkUpsertResult();
            if (!keys.Any()) return result;

            var database = _dbContextProvider.GetDatabase(tenantId);
            var collection = database.GetCollection<BlocksLanguageKey>(_keysCollectionName);

            if (shouldOverwrite)
            {
                // Replace existing + insert new
                var bulkOps = keys.Select(key =>
                {
                    var filter = Builders<BlocksLanguageKey>.Filter.Eq(k => k.ItemId, key.ItemId);
                    return new ReplaceOneModel<BlocksLanguageKey>(filter, key) { IsUpsert = true };
                }).ToList();

                var bulkResult = await collection.BulkWriteAsync(bulkOps);
                
                // When shouldOverwrite is true, all keys are considered "upserted" for timeline purposes
                result.UpsertedKeys = keys.ToList();
                result.InsertedKeys = keys.Where(k => bulkResult.Upserts.Any(u => u.Id == k.ItemId)).ToList();
                result.UpdatedKeys = keys.Except(result.InsertedKeys).ToList();
            }
            else
            {
                // Get existing keys first to determine what will actually be inserted
                var keyIds = keys.Select(k => k.ItemId).ToList();
                var existingKeys = await GetExistingKeysByItemIdsAsync(keyIds, tenantId);
                var existingKeyIds = existingKeys.Select(k => k.ItemId).ToHashSet();
                
                // Only keys that don't exist will be inserted
                var keysToInsert = keys.Where(k => !existingKeyIds.Contains(k.ItemId)).ToList();
                
                if (keysToInsert.Any())
                {
                    // Only insert new (use $setOnInsert to avoid overwriting existing data)
                    var bulkOps = keys.Select(key =>
                    {
                        var filter = Builders<BlocksLanguageKey>.Filter.Eq(k => k.ItemId, key.ItemId);
                        var update = Builders<BlocksLanguageKey>.Update
                            .SetOnInsert(k => k.ItemId, key.ItemId)
                            .SetOnInsert(k => k.KeyName, key.KeyName)
                            .SetOnInsert(k => k.ModuleId, key.ModuleId)
                            .SetOnInsert(k => k.Value, key.Value)
                            .SetOnInsert(k => k.Resources, key.Resources)
                            .SetOnInsert(k => k.Routes, key.Routes)
                            .SetOnInsert(k => k.IsPartiallyTranslated, key.IsPartiallyTranslated)
                            .SetOnInsert(k => k.CreateDate, key.CreateDate)
                            .SetOnInsert(k => k.LastUpdateDate, key.LastUpdateDate)
                            .SetOnInsert(k => k.TenantId, key.TenantId)
                            .SetOnInsert(k => k.CreatedBy, key.CreatedBy)
                            .SetOnInsert(k => k.LastUpdatedBy, key.LastUpdatedBy);
                        
                        return new UpdateOneModel<BlocksLanguageKey>(filter, update) { IsUpsert = true };
                    }).ToList();

                    await collection.BulkWriteAsync(bulkOps);
                }
                
                // Only the keys that were actually inserted should be in the result
                result.InsertedKeys = keysToInsert;
                result.UpsertedKeys = keysToInsert; // For timeline creation, we only care about inserted keys when shouldOverwrite is false
            }

            return result;
        }
    }
}
