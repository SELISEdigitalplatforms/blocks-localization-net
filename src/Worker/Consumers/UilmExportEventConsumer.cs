using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;

namespace Worker.Consumers
{
    public class UilmExportEventConsumer : IConsumer<UilmExportEvent>
    {
        private readonly IKeyManagementService _keyManagementService;

        public UilmExportEventConsumer(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
        }
        public async Task Consume(UilmExportEvent @event)
        {
            var isSuccess = await _keyManagementService.ExportUilmFile(@event);
        }
    }
}