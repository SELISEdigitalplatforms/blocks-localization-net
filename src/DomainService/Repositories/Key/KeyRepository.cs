using Blocks.Genesis;
using DomainService.Services;
using MongoDB.Bson;
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

        public async Task<List<Key>> GetAllKeysAsync(GetKeysQuery query)
        {
            var collection = _dbContextProvider.GetCollection<Key>(_collectionName);

            var filter = getAllKeysFilter(query);

            var sort = !string.IsNullOrWhiteSpace(query.SortProperty) && query.IsDescending ? Builders<Key>.Sort.Descending(query.SortProperty) : Builders<Key>.Sort.Ascending(query.SortProperty ?? "KeyName");

            return await collection
                                        .Find(filter)
                                        .Skip(query.PageNumber*query.PageSize)
                                        .Sort(sort)
                                        .ToListAsync();
        }

        private static FilterDefinition<Key> getAllKeysFilter(GetKeysQuery query)
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
            return matchFilters.Count > 0 ? filterBuilder.And(matchFilters): filterBuilder.Empty;
        }

        private static List<FilterDefinition<Key>> setDateFilter(GetKeysQuery query, FilterDefinitionBuilder<Key> filterBuilder)
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

        public async Task<BlocksLanguageKey> GetKeyByNameAsync(string KeyName)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, KeyName);

            return await collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task SaveKeyAsync(BlocksLanguageKey key)
        {
            var dataBase = _dbContextProvider.GetDatabase(_tenantId);
            var collection = dataBase.GetCollection<BlocksLanguageKey>(_collectionName);

            var filter = Builders<BlocksLanguageKey>.Filter.Eq(mc => mc.KeyName, key.KeyName);

            await collection.ReplaceOneAsync(
                filter,
                key,
                new ReplaceOptions { IsUpsert = true });
        }
    }
}
