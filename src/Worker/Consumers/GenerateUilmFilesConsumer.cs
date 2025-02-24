using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;

namespace Worker.Consumers
{
    public class GenerateUilmFilesConsumer : IConsumer<GenerateUilmFilesEvent>
    {
        private readonly IKeyManagementService _keyManagementService;

        public GenerateUilmFilesConsumer(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
        }
        public async Task Consume(GenerateUilmFilesEvent context)
        {
            await _keyManagementService.GenerateAsync(context);
        }
    }
}
