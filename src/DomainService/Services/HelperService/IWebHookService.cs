using DomainService.Shared;
using DomainService.Shared.Entities;

namespace DomainService.Services.HelperService
{
    public interface IWebHookService
    {
        Task<bool> CallWebhook(object payload);
        Task<ApiResponse> SaveWebhookAsync(BlocksWebhook webhook);
    }
}