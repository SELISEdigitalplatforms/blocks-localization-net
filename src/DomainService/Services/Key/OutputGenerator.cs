using DomainService.Repositories;
using DomainService.Shared.Entities;

namespace DomainService.Services
{
    public abstract class OutputGenerator
    {
        public abstract Task<T> GenerateAsync<T>(BlocksLanguage languageSetting, List<BlocksLanguageModule> applications,
            List<BlocksLanguageResourceKey> resourceKeys, string defaultLanguage);
    }
}
