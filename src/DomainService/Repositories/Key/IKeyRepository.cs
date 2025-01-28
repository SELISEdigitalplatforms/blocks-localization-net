using DomainService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainService.Repositories
{
    public interface IKeyRepository
    {
        Task SaveKeyAsync(BlocksLanguageKey key);
        Task<BlocksLanguageKey> GetKeyByNameAsync(string KeyName);
        Task<GetKeysQueryResponse> GetAllKeysAsync(GetKeysRequest query);
    }
}
