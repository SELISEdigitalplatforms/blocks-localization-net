using Blocks.Genesis;

namespace DomainService.Services
{
    public class GetKeyRequest : IProjectKey
    {
        public string ItemId { get; set; }
        public string? ProjectKey { get; set; }
    }
}
