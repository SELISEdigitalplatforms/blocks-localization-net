using Blocks.Genesis;

namespace DomainService.Services
{
    public class GetLanguagesRequest : IProjectKey
    {
        public string? ProjectKey { get; set; }
    }
}
