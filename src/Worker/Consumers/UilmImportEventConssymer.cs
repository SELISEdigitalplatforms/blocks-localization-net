using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;

namespace Worker.Consumers
{

    public class UilmImportEventConsumer : IConsumer<UilmImportEvent>
    {
        private readonly IKeyManagementService _keyManagementService;

        public UilmImportEventConsumer(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
        }
        public async Task Consume(UilmImportEvent @event)
        {
            var isSuccess = await _keyManagementService.ImportUilmFile(@event);
        }
    }
}