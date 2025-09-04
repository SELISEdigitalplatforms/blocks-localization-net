using Blocks.Genesis;
using DomainService.Services;
using MongoDB.Driver;

namespace DomainService.Repositories
{
    public class KeyTimelineRepository : IKeyTimelineRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private const string _collectionName = "KeyTimelines";

        public KeyTimelineRepository(IDbContextProvider dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<GetKeyTimelineQueryResponse> GetKeyTimelineAsync(GetKeyTimelineRequest query)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<KeyTimeline>(_collectionName);

            var filter = GetTimelineFilter(query);
            var sort = !string.IsNullOrWhiteSpace(query.SortProperty) && query.IsDescending 
                ? Builders<KeyTimeline>.Sort.Descending(query.SortProperty) 
                : Builders<KeyTimeline>.Sort.Ascending(query.SortProperty ?? "CreateDate");

            var totalCount = await collection.CountDocumentsAsync(filter);
            
            var timelines = await collection
                .Find(filter)
                .Sort(sort)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Limit(query.PageSize)
                .ToListAsync();

            return new GetKeyTimelineQueryResponse
            {
                TotalCount = totalCount,
                Timelines = timelines
            };
        }

        public async Task SaveKeyTimelineAsync(KeyTimeline timeline)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<KeyTimeline>(_collectionName);

            if (string.IsNullOrEmpty(timeline.ItemId))
            {
                timeline.ItemId = Guid.NewGuid().ToString();
                timeline.CreateDate = DateTime.Now;
                timeline.LastUpdateDate = DateTime.Now;
                await collection.InsertOneAsync(timeline);
            }
            else
            {
                timeline.LastUpdateDate = DateTime.Now;
                var filter = Builders<KeyTimeline>.Filter.Eq(t => t.ItemId, timeline.ItemId);
                await collection.ReplaceOneAsync(filter, timeline, new ReplaceOptions { IsUpsert = true });
            }
        }

        private FilterDefinition<KeyTimeline> GetTimelineFilter(GetKeyTimelineRequest request)
        {
            var builder = Builders<KeyTimeline>.Filter;
            var filters = new List<FilterDefinition<KeyTimeline>>();

            // Filter by EntityId (Key ItemId)
            if (!string.IsNullOrWhiteSpace(request.EntityId))
            {
                filters.Add(builder.Eq(t => t.EntityId, request.EntityId));
            }

            // Filter by UserId
            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                filters.Add(builder.Eq(t => t.UserId, request.UserId));
            }

            // Filter by CreateDate range
            if (request.CreateDateRange != null)
            {
                if (request.CreateDateRange.StartDate.HasValue)
                {
                    filters.Add(builder.Gte(t => t.CreateDate, request.CreateDateRange.StartDate.Value));
                }
                if (request.CreateDateRange.EndDate.HasValue)
                {
                    filters.Add(builder.Lte(t => t.CreateDate, request.CreateDateRange.EndDate.Value));
                }
            }

            return filters.Count > 0 ? builder.And(filters) : builder.Empty;
        }
    }
}
