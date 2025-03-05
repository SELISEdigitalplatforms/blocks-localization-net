using Blocks.Genesis;

namespace DomainService.Services
{
    public class TranslateAllRequest : IProjectKey
    {
        public string MessageCoRelationId { get; set; }
        public string ProjectKey { get; set; }
        public string DefaultLanguage { get; set; }
    }
}
