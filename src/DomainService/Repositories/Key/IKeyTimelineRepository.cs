using DomainService.Services;

namespace DomainService.Repositories
{
    public interface IKeyTimelineRepository
    {
        Task<GetKeyTimelineQueryResponse> GetKeyTimelineAsync(GetKeyTimelineRequest query);
        Task SaveKeyTimelineAsync(KeyTimeline timeline);
        Task<KeyTimeline?> GetTimelineByItemIdAsync(string itemId);
    }
}
