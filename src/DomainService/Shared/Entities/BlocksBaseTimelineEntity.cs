using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Shared.Entities
{
    [BsonIgnoreExtraElements]
    public class BlocksBaseTimelineEntity<CT, PT>
    {
        [BsonId]
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string ClientTenantId { get; set; }
        public string ClientSiteId { get; set; }
        public string OrganizationId { get; set; }
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public DateTime LastUpdateDate { get; set; } = DateTime.Now;
        public CT CurrentData { get; set; }
        public PT PreviousData { get; set; }
        public string LogFrom { get; set; }
        public string UserId { get; set; }
        public string RollbackFrom { get; set; }
    }
}
