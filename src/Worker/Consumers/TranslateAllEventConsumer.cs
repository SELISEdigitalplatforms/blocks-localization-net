using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;

namespace Worker.Consumers
{
    public class TranslateAllEventConsumer : IConsumer<TranslateAllEvent>
    {
        private readonly IKeyManagementService _keyManagementService;

        public TranslateAllEventConsumer(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
        }
        public async Task Consume(TranslateAllEvent @event)
        {
            await _keyManagementService.ChangeAll(@event);
        }
    }
}
