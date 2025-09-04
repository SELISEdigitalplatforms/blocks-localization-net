using DomainService.Shared.Entities;
using DomainService.Repositories;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Services
{
    [BsonIgnoreExtraElements]
    public class KeyTimeline : BlocksBaseTimelineEntity<BlocksLanguageKey, BlocksLanguageKey>
    {
    }
}
