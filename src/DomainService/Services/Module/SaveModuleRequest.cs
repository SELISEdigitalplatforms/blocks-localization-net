using Blocks.Genesis;

namespace DomainService.Services
{
    public class SaveModuleRequest : Module, IProjectKey
    {
        public string? ProjectKey { get; set; }
    }
}
