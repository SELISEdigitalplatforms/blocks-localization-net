using DomainService.Shared;

namespace DomainService.Repositories
{
    public class BlocksLanguageModule : BaseEntity
    {
        public string ModuleName { get; set; }
        public string Name { get; set; }
    }
}
