using DomainService.Shared;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Repositories
{
    [BsonIgnoreExtraElements]
    public class BlocksGlossary : BaseEntity
    {
        public string Name { get; set; }
        public string Language { get; set; }
        public string Type { get; set; }
        public string Context { get; set; }
        public string AdditionalNote { get; set; }
        public string? Scope { get; set; }
        public List<string>? ModuleIds { get; set; }
    }
}
