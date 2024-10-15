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
    public class LanguageRepository : ILanguageRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";
        private const string _collectionName = "BlocksLanguages";

        public LanguageRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<List<Language>> GetAllLanguagesAsync()
        {
            var collection = _dbContextProvider.GetCollection<Language>(_collectionName);
            var command = async () => await collection.Find(_ => true).ToListAsync();

            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task<BlocksLanguage> GetLanguageByNameAsync(string languageName)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguage>(_collectionName);

            var filter = Builders<BlocksLanguage>.Filter.Eq(mc => mc.LanguageName, languageName);

            var command = async () => await collection.Find(filter).FirstOrDefaultAsync();
            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task SaveAsync(BlocksLanguage language)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguage>(_collectionName);

            var filter = Builders<BlocksLanguage>.Filter.And(
                          Builders<BlocksLanguage>.Filter.Eq(mc => mc.LanguageName, language.LanguageName),
                          Builders<BlocksLanguage>.Filter.Eq(mc => mc.LanguageCode, language.LanguageCode));

            var command = async () => await collection.ReplaceOneAsync(
                filter,
                language,
                new ReplaceOptions { IsUpsert = true }
            );

            await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Save", command);
        }
    }
}
