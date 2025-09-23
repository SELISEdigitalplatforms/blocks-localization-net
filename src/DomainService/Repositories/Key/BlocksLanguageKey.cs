using DomainService.Services;
using DomainService.Shared;

namespace DomainService.Repositories
{
    public class BlocksLanguageKey : BaseEntity
    {
        public string KeyName { get; set; }
        public string ModuleId { get; set; }
        public string Value { get; set; }
        public Resource[] Resources { get; set; }
        public List<string> Routes { get; set; }
        public string? Context { get; set; }
        public bool IsPartiallyTranslated { get; set; }
    }
}
