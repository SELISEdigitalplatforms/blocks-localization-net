using Blocks.Genesis;
using DomainService.Services;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainService.Repositories
{
    public class KeyRepository : IKeyRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";
        private const string _collectionName = "BlocksLanguageKeys";

        public KeyRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<List<Key>> GetAllKeysAsync()
        {
            var collection = _dbContextProvider.GetCollection<Key>(_collectionName);
            var command = async () => await collection.Find(_ => true).ToListAsync();

            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task<BlocksLanguageKey> GetKeyByNameAsync(string KeyName)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, KeyName);

            var command = async () => await collection.Find(filter).FirstOrDefaultAsync();
            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task SaveKeyAsync(BlocksLanguageKey key)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, key.KeyName);

            var command = async () => await collection.ReplaceOneAsync(
                filter,
                key,
                new ReplaceOptions { IsUpsert = true });

            await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Save", command);
        }
    }
}
