using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Shared;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DomainService.Services
{
    public class LanguageManagementService : ILanguageManagementService
    {
        private readonly IValidator<Language> _validator;
        private readonly ILogger<LanguageManagementService> _logger;
        private readonly ILanguageRepository _languageRepository;

        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";

        public LanguageManagementService(IValidator<Language> validator,
                                        ILogger<LanguageManagementService> logger,
                                        ILanguageRepository languageRepository)
        {
            _validator = validator;
            _logger = logger;
            _languageRepository = languageRepository;
        }

        public async Task<ApiResponse> SaveLanguageAsync(Language language)
        {
            var validationResult = await _validator.ValidateAsync(language);

            if (!validationResult.IsValid)
                return new ApiResponse(string.Empty, validationResult.Errors);

            try
            {
                var repoLanguage = await MappedIntoRepoLanguageAsync(language);
                await _languageRepository.SaveAsync(repoLanguage);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while saving BlocksLanguage {errorMessage} : {StackTrace}", ex.Message, ex.StackTrace);
                return new ApiResponse(ex.Message);
            }

            return new ApiResponse();
        }

        public async Task<List<Language>> GetLanguagesAsync()
        {
            return await _languageRepository.GetAllLanguagesAsync();
        }

        private async Task<BlocksLanguage> MappedIntoRepoLanguageAsync(Language language)
        {
            var repoLanguage = await _languageRepository.GetLanguageByNameAsync(language.LanguageName);

            if(repoLanguage == null)
                repoLanguage = new BlocksLanguage { ItemId = Guid.NewGuid().ToString(), CreateDate = DateTime.UtcNow, TenantId = _tenantId};

            repoLanguage.LastUpdateDate = DateTime.UtcNow;
            repoLanguage.LanguageCode = language.LanguageCode;
            repoLanguage.LanguageName = language.LanguageName;
            repoLanguage.IsDefault = language.IsDefault;
            repoLanguage.ItemId = language.ItemId;

            return repoLanguage;
        }
    }
}
