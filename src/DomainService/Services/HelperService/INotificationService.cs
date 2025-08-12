namespace DomainService.Services.HelperService
{
    public interface INotificationService
    {
        Task<bool> NotifyExportEvent(bool response, string fileId, string? messageCoRelationId, string tenantId);
        Task<bool> NotifyTranslateAllEvent(bool response, string? messageCoRelationId);
    }
}
