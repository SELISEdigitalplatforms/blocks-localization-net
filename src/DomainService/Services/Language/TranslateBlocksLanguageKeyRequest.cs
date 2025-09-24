using Blocks.Genesis;

namespace DomainService.Services
{
    public class TranslateBlocksLanguageKeyRequest : IProjectKey
    {
        public required string KeyId { get; set; }
        public required string MessageCoRelationId { get; set; }
        public required string ProjectKey { get; set; }
        public required string DefaultLanguage { get; set; }
    }
}