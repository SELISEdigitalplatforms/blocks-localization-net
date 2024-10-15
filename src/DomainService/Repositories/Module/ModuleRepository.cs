using Blocks.Genesis;
using DomainService.Services;
using MongoDB.Driver;
using Polly;



namespace DomainService.Repositories
{
    public class ModuleRepository : IModuleRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";
        private const string _collectionName = "BlocksLanguageModules";

        public ModuleRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<BlocksLanguageModule> GetByNameAsync(string name)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageModule>(_collectionName);

            var filter = Builders<BlocksLanguageModule>.Filter.Eq(mc => mc.ModuleName, name);
            var command = async () => await collection.Find(filter).FirstOrDefaultAsync();

            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task<List<Module>> GetAllAsync()
        {
            var collection = _dbContextProvider.GetCollection<Module>(_collectionName);
            var command = async () => await collection.Find(_ => true).ToListAsync();

            return await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Find", command);
        }

        public async Task SaveAsync(BlocksLanguageModule module)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageModule>(_collectionName);

            var filter = Builders<BlocksLanguageModule>.Filter.Eq(mc => mc.ModuleName, module.ModuleName);

            var command = async () => await collection.ReplaceOneAsync(
                filter,
                module,
                new ReplaceOptions { IsUpsert = true }
            );

            await _dbContextProvider.RunMongoCommandWithActivityAsync(_collectionName, "Save", command);

        }
    }
}
