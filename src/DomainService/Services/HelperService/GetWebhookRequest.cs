using Blocks.Genesis;

namespace DomainService.Services.HelperService
{
    public class GetWebhookRequest : IProjectKey
    {
        public required string ProjectKey { get; set; }
    }
}
