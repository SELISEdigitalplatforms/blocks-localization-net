using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Services
{
    [BsonIgnoreExtraElements]
    public class Module
    {
        public string ModuleName { get; set; }
    }
}
