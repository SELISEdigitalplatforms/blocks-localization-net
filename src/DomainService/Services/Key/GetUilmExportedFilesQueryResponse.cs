using DomainService.Repositories;

namespace DomainService.Services
{
    public class GetUilmExportedFilesQueryResponse
    {
        public long TotalCount { get; set; }
        public List<UilmExportedFile> UilmExportedFiles { get; set; } = new();
    }
}
