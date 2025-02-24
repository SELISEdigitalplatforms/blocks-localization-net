using DomainService.Shared;
using DomainService.Shared.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainService.Services
{
    public interface IKeyManagementService
    {
        Task<ApiResponse> SaveKeyAsync(Key key);
        Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query);
        Task<bool> GenerateAsync(GenerateUilmFilesEvent command);
        Task<string> GetUilmFile(GetUilmFileRequest request);
    }
}
