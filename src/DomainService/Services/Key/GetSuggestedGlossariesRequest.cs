using Blocks.Genesis;
using DomainService.Shared;

namespace DomainService.Services
{
    public class GetSuggestedGlossariesRequest : IProjectKey
    {
        public string ItemId { get; set; }
        public string? ProjectKey { get; set; }
        public int MaxResults { get; set; } = 5;
    }
}
