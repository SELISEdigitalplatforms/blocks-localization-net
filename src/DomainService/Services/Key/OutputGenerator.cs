using DomainService.Repositories;
using DomainService.Shared.Entities;

namespace DomainService.Services
{
    public abstract class OutputGenerator
    {
        public abstract Task<T> GenerateAsync<T>(List<BlocksLanguage> languageSettings, List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string defaultLanguage);
    }
}
