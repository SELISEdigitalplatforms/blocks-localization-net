using DomainService.Shared.DTOs;
using MongoDB.Bson.Serialization.Attributes;

namespace DomainService.Shared.Entities
{
    [BsonIgnoreExtraElements]
    public class BlocksLanguageManagerTimeline : BlocksBaseTimelineEntity<LanguageManagerDto, LanguageManagerDto>
    {
    }
}
