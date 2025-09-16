using DomainService.Services;

namespace DomainService.Repositories
{
    public interface IKeyTimelineRepository
    {
        Task<GetKeyTimelineQueryResponse> GetKeyTimelineAsync(GetKeyTimelineRequest query);
        Task SaveKeyTimelineAsync(KeyTimeline timeline);
        Task BulkSaveKeyTimelinesAsync(List<KeyTimeline> timelines, string targetedProjectKey);
        Task<KeyTimeline?> GetTimelineByItemIdAsync(string itemId);
    }
}
