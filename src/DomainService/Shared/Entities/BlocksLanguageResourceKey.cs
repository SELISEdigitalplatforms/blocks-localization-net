using DomainService.Repositories;

namespace DomainService.Shared.Entities
{
    public class BlocksLanguageResourceKey : BlocksLanguageKey
    {
        public string OrganizationId { get; set; }
        public string ActualId { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
    }
}