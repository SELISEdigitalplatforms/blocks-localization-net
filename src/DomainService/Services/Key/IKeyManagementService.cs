using Blocks.Genesis;
using DomainService.Shared;
using DomainService.Shared.Events;

namespace DomainService.Services
{
    public interface IKeyManagementService
    {
        Task<ApiResponse> SaveKeyAsync(Key key);
        Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query);
        Task<GetKeyTimelineQueryResponse> GetKeyTimelineAsync(GetKeyTimelineRequest query);
        Task<bool> GenerateAsync(GenerateUilmFilesEvent command);
        Task<string> GetUilmFile(GetUilmFileRequest request);
        Task<Key?> GetAsync(GetKeyRequest request);
        Task<BaseMutationResponse> DeleteAsysnc(DeleteKeyRequest request);
        Task SendTranslateAllEvent(TranslateAllRequest request);
        Task SendUilmImportEvent(UilmImportRequest request);
        Task SendUilmExportEvent(UilmExportRequest request);
        Task SendGenerateUilmFilesEvent(GenerateUilmFilesRequest request);
        Task<bool> ChangeAll(TranslateAllEvent request);
        Task<bool> ImportUilmFile(UilmImportEvent request);
        Task<bool> ExportUilmFile(UilmExportEvent request);
        Task PublishUilmExportNotification(bool response, string fileId, string? messageCoRelationId, string tenantId);
        Task PublishTranslateAllNotification(bool response, string? messageCoRelationId);
        Task<BaseMutationResponse> DeleteCollectionsAsync(DeleteCollectionsRequest request);
    }
}
