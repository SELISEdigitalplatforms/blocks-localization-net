using Blocks.Genesis;

namespace DomainService.Services.Assistant
{
    public class AiCompletionRequest : IProjectKey
    {
        public string Message { get; set; }
        public double Temperature { get; set; }
        public string? ProjectKey { get; set; }
    }
}
