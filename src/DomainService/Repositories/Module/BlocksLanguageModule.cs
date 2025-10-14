using DomainService.Shared;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Repositories
{
    [BsonIgnoreExtraElements]
    public class BlocksLanguageModule : BaseEntity
    {
        public string ModuleName { get; set; }
        public string Name { get; set; }
    }
}
