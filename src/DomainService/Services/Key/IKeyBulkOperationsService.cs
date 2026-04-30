namespace DomainService.Services
{
    public interface IKeyBulkOperationsService
    {
        Task BulkDeleteByModuleAsync(string moduleId, string projectKey);
        Task BulkMoveByModuleAsync(string fromModuleId, string toModuleId, string projectKey);
    }
}
