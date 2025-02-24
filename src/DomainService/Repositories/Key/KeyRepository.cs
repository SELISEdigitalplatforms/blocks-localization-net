using Blocks.Genesis;
using DomainService.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DomainService.Repositories
{
    public class KeyRepository : IKeyRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private const string _collectionName = "BlocksLanguageKeys";

        public KeyRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<GetKeysQueryResponse> GetAllKeysAsync(GetKeysRequest request)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<Key>(_collectionName);

            var filter = getAllKeysFilter(request);

            var sort = !string.IsNullOrWhiteSpace(request.SortProperty) && request.IsDescending ? Builders<Key>.Sort.Descending(request.SortProperty) : Builders<Key>.Sort.Ascending(request.SortProperty ?? "KeyName");

            var findKeysTask = collection
                .Find(filter)
                .Skip(request.PageNumber * request.PageSize)
                .Limit(request.PageSize)
                .Sort(sort)
                .ToListAsync();

            var countDocumentsTask = collection.CountDocumentsAsync(filter);

            await Task.WhenAll(findKeysTask, countDocumentsTask);

            return new GetKeysQueryResponse
            {
                Keys = findKeysTask.Result,
                TotalCount = countDocumentsTask.Result
            };
        }

        private static FilterDefinition<Key> getAllKeysFilter(GetKeysRequest query)
        {
            var filterBuilder = Builders<Key>.Filter;
            var matchFilters = new List<FilterDefinition<Key>>();
            if (!string.IsNullOrWhiteSpace(query.KeySearchText))
            {
                matchFilters.Add(filterBuilder.Regex("KeyName", new BsonRegularExpression($".*{query.KeySearchText}.*", "i")));
            }
            if (query.ModuleIds != null && query.ModuleIds.Length > 0)
            {
                matchFilters.Add(filterBuilder.In(x => x.ModuleId, query.ModuleIds));
            }

            if (query.CreateDateRange != null)
            {
                List<FilterDefinition<Key>> dateFilters = setDateFilter(query, filterBuilder);
                if (dateFilters.Count > 0)
                {
                    matchFilters.Add(filterBuilder.And(dateFilters));
                }
            }
            return matchFilters.Count > 0 ? filterBuilder.And(matchFilters) : filterBuilder.Empty;
        }

        private static List<FilterDefinition<Key>> setDateFilter(GetKeysRequest query, FilterDefinitionBuilder<Key> filterBuilder)
        {
            FilterDefinition<Key> dateFilter;
            var dateFilters = new List<FilterDefinition<Key>>();

            if (query.CreateDateRange.StartDate != default(DateTime) && query.CreateDateRange.StartDate != null && (query.CreateDateRange.EndDate == default(DateTime) || query.CreateDateRange.EndDate == null))
            {
                dateFilter = filterBuilder.Gte("CreateDate", query.CreateDateRange.StartDate);
                dateFilters.Add(dateFilter);
            }
            else if ((query.CreateDateRange.StartDate == default(DateTime) || query.CreateDateRange.StartDate == null) && query.CreateDateRange.EndDate != default(DateTime) && query.CreateDateRange.EndDate != null)
            {
                dateFilter = filterBuilder.Lte("CreateDate", query.CreateDateRange.EndDate);
                dateFilters.Add(dateFilter);
            }
            else if (query.CreateDateRange.StartDate != default(DateTime) && query.CreateDateRange.StartDate != null && query.CreateDateRange.EndDate != default(DateTime) && query.CreateDateRange.EndDate != null)
            {
                dateFilter = filterBuilder.And(
                    filterBuilder.Gte("CreateDate", query.CreateDateRange.StartDate),
                    filterBuilder.Lte("CreateDate", query.CreateDateRange.EndDate)
                );
                dateFilters.Add(dateFilter);
            }
            return dateFilters;
        }

        public async Task<BlocksLanguageKey> GetKeyByNameAsync(string KeyName, string moduleId)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, KeyName) &
                     Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.ModuleId, moduleId);

            return await collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task SaveKeyAsync(BlocksLanguageKey key)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, key.KeyName);

            await collection.ReplaceOneAsync(
                filter,
                key,
                new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<Key>> GetAllKeysByModuleAsync(string moduleId)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<Key>(_collectionName);

            var filterBuilder = Builders<Key>.Filter;
            var matchFilters = new List<FilterDefinition<Key>>
            {
                filterBuilder.Eq(x => x.ModuleId, moduleId)
            };
            var filter = filterBuilder.And(matchFilters);

            return await collection
                .Find(filter)
                .ToListAsync();
        }

        public async Task<bool> SaveNewUilmFiles(List<UilmFile> uilmfiles)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            await dataBase.GetCollection<UilmFile>($"{nameof(UilmFile)}s")
                .InsertManyAsync(uilmfiles);

            return true;
        }

        public async Task<long> DeleteOldUilmFiles(List<UilmFile> uilmfiles)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var modules = uilmfiles.Select(x => x.ModuleName).Distinct();
            var filter = Builders<UilmFile>.Filter.In(x => x.ModuleName, modules);
            var result = await dataBase.GetCollection<UilmFile>($"{nameof(UilmFile)}s")
                .DeleteManyAsync(filter);

            return result.DeletedCount;
        }

        public async Task<UilmFile> GetUilmFile(GetUilmFileRequest request)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var project = Builders<BsonDocument>.Projection.As<UilmFile>();
            var filter = Builders<BsonDocument>.Filter.Eq("Language", request.Language) & Builders<BsonDocument>.Filter.Eq("ModuleName", request.ModuleName);

            return await dataBase.GetCollection<BsonDocument>("UilmFiles")
                .Find(filter)
                .Project(project)
                .FirstOrDefaultAsync();
        }
    }
}
