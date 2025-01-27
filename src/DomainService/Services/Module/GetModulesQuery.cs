using Blocks.Genesis;

namespace DomainService.Services
{
    public class GetModulesQuery : IProjectKey
    {
        public string? ProjectKey { get; set; }
    }
}
