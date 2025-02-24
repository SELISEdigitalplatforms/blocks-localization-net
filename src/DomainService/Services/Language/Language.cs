using Blocks.Genesis;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Services
{
    [BsonIgnoreExtraElements]
    public class Language : IProjectKey
    {
        [BsonId]
        public string ItemId { get; set; }
        public string LanguageName { get; set; }
        public string LanguageCode { get; set; }
        public bool IsDefault { get; set; }
        public string? ProjectKey { get; set; }
    }
}
