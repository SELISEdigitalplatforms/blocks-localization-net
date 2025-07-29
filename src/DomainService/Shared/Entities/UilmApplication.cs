using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Shared.Entities
{
    [BsonIgnoreExtraElements]
    public class UilmApplication
    {
        [BsonId]
        public string Id { get; set; }
        public string Name { get; set; }
        public string AppPath { get; set; }
        public int NumberOfKeys { get; set; }
        public string ModuleName { get; set; }
    }
}
