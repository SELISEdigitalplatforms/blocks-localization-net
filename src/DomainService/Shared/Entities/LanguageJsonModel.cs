using DomainService.Services;

namespace DomainService.Shared.Entities
{
    public class LanguageJsonModel
    {
        public string Id { get; set; }
        public string AppId { get; set; }
        public string Type { get; set; }
        public string App { get; set; }
        public string Module { get; set; }
        public string Key { get; set; }
        public Resource[] Resources { get; set; }
    }
}
