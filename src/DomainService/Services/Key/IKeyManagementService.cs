using DomainService.Shared;
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
    }
}
