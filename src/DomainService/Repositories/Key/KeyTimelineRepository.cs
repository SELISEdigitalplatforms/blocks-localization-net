using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace DomainService.Repositories
{
    public class KeyTimelineRepository : IKeyTimelineRepository
    {
        private readonly IDbContextProvider _dbContextProvider;
        private readonly IConfiguration _configuration;
        private const string _collectionName = "KeyTimelines";

        public KeyTimelineRepository(IDbContextProvider dbContextProvider, IConfiguration configuration)
        {
            _dbContextProvider = dbContextProvider;
            _configuration = configuration;
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

            // Get unique user IDs from timelines
            var uniqueUserIds = timelines
                .Where(t => !string.IsNullOrEmpty(t.UserId))
                .Select(t => t.UserId)
                .Distinct()
                .ToList();

            // Fetch user information if there are any user IDs
            Dictionary<string, User> userLookup = new Dictionary<string, User>();
            if (uniqueUserIds.Any())
            {
                var rootTenantId = _configuration["RootTenantId"];
                var rootDB = _dbContextProvider.GetDatabase(rootTenantId);
                var usersCollection = rootDB.GetCollection<User>("Users");
                var userFilter = Builders<User>.Filter.In(u => u.ItemId, uniqueUserIds);
                var users = await usersCollection.Find(userFilter).ToListAsync();

                userLookup = users.ToDictionary(u => u.ItemId, u => u);
            }

            // Populate UserName property for each timeline
            foreach (var timeline in timelines)
            {
                if (!string.IsNullOrEmpty(timeline.UserId) && userLookup.TryGetValue(timeline.UserId, out var user))
                {
                    // Use FirstName + LastName if available, otherwise use Email
                    if (!string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName))
                    {
                        var firstName = user.FirstName ?? "";
                        var lastName = user.LastName ?? "";
                        timeline.UserName = $"{firstName} {lastName}".Trim();
                    }
                    else if (!string.IsNullOrEmpty(user.Email))
                    {
                        timeline.UserName = user.Email;
                    }
                    else
                    {
                        timeline.UserName = timeline.UserId; // Fallback to UserId
                    }
                }
                else
                {
                    timeline.UserName = timeline.UserId ?? "Unknown"; // Fallback
                }
            }

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

        public async Task BulkSaveKeyTimelinesAsync(List<KeyTimeline> timelines, string targetedProjectKey)
        {
            if (!timelines.Any()) return;

            var dataBase = _dbContextProvider.GetDatabase(targetedProjectKey);
            var collection = dataBase.GetCollection<KeyTimeline>(_collectionName);

            // Prepare timelines for bulk insert
            var now = DateTime.UtcNow;
            foreach (var timeline in timelines)
            {
                if (string.IsNullOrEmpty(timeline.ItemId))
                {
                    timeline.ItemId = Guid.NewGuid().ToString();
                }
                timeline.CreateDate = now;
                timeline.LastUpdateDate = now;
            }

            // Use InsertManyAsync for bulk insertion
            await collection.InsertManyAsync(timelines);
        }

        public async Task<KeyTimeline?> GetTimelineByItemIdAsync(string itemId)
        {
            var dataBase = _dbContextProvider.GetDatabase(BlocksContext.GetContext()?.TenantId ?? "");
            var collection = dataBase.GetCollection<KeyTimeline>(_collectionName);

            var filter = Builders<KeyTimeline>.Filter.Eq(t => t.ItemId, itemId);

            return await collection.Find(filter).FirstOrDefaultAsync();
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
