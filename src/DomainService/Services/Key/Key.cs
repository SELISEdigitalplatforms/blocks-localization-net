using DomainService.Dtos;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Services
{
    [BsonIgnoreExtraElements]
    public class Key : IProjectKey
    {
        public string KeyName { get; set; }
        public string ModuleId { get; set; }
        public string Value { get; set; }
        public Resource[] Resources { get; set; }
        public List<string> Routes { get; set; }
        public bool IsPartiallyTranslated { get; set; }
        public string? ProjectKey { get; set; }
    }
}
