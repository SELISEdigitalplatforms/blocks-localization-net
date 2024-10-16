using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Services
{
    [BsonIgnoreExtraElements]
    public class Key
    {
        public string KeyName { get; set; }
        public string Module { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> Translations { get; set; }
        public List<string> Routes { get; set; }
        public bool IsPartiallyTranslated { get; set; }
    }
}
