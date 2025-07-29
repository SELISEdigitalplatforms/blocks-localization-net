using DomainService.Repositories;
using DomainService.Shared.Entities;

namespace DomainService.Services
{
    public abstract class OutputGenerator
    {
        public abstract Task<T> GenerateAsync<T>(BlocksLanguage languageSetting, List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string defaultLanguage);
    }
}
