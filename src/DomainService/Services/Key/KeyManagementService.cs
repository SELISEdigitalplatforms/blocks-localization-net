using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Shared;
using FluentValidation;
using Microsoft.Extensions.Logging;


namespace DomainService.Services
{
    public class KeyManagementService : IKeyManagementService
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IValidator<Key> _validator;
        private readonly ILogger<KeyManagementService> _logger; 

        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";

        public KeyManagementService(IKeyRepository keyRepository,
                                    IValidator<Key> validator,
                                    ILogger<KeyManagementService> logger)
        {
            _keyRepository = keyRepository;
            _validator = validator;
            _logger = logger;
        }

        public async Task<ApiResponse> SaveKeyAsync(Key key)
        {
            var validationResult = await _validator.ValidateAsync(key);

            if (!validationResult.IsValid)
                return new ApiResponse(string.Empty, validationResult.Errors);

            try
            {
                var repoKey = await MappedIntoRepoKeyAsync(key);
                await _keyRepository.SaveKeyAsync(repoKey);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while saving BlocksLanguage {errorMessage} : {StackTrace}", ex.Message, ex.StackTrace);
                return new ApiResponse(ex.Message);
            }

            return new ApiResponse();
        }

        private async Task<BlocksLanguageKey> MappedIntoRepoKeyAsync(Key key)
        {
            var repoKey = await _keyRepository.GetKeyByNameAsync(key.KeyName);

            if (repoKey == null)
                repoKey = new BlocksLanguageKey { ItemId = new Guid().ToString(), CreateDate = DateTime.UtcNow, TenantId = _tenantId };

            repoKey.LastUpdateDate = DateTime.UtcNow;
            repoKey.KeyName = key.KeyName;
            repoKey.Value = key.Value;
            repoKey.Translations = key.Translations;
            repoKey.IsPartiallyTranslated = key.IsPartiallyTranslated;

            return repoKey;
        }

        public async Task<List<Key>> GetKeysAsync()
        {
            return await _keyRepository.GetAllKeysAsync();
        }
    }
}
