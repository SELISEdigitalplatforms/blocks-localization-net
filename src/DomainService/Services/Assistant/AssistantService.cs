using Microsoft.Extensions.Logging;

namespace DomainService.Services.Assistant
{
    public class AssistantService
    {
        private readonly ILogger<AssistantService> _logger;
        public AssistantService(
            ILogger<AssistantService> logger
        ) 
        { 
            _logger = logger;
        }
    }
}
