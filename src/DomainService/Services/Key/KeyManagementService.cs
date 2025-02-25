using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Shared;
using DomainService.Shared.Events;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace DomainService.Services
{
    public class KeyManagementService : IKeyManagementService
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IValidator<Key> _validator;
        private readonly ILogger<KeyManagementService> _logger;
        private readonly ILanguageManagementService _languageManagementService;
        private readonly IModuleManagementService _moduleManagementService;

        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";

        public KeyManagementService(
            IKeyRepository keyRepository,
            IValidator<Key> validator,
            ILogger<KeyManagementService> logger,
            ILanguageManagementService languageManagementService,
            IModuleManagementService moduleManagementService)
        {
            _keyRepository = keyRepository;
            _validator = validator;
            _logger = logger;
            _languageManagementService = languageManagementService;
            _moduleManagementService = moduleManagementService;
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
            var repoKey = await _keyRepository.GetKeyByNameAsync(key.KeyName, key.ModuleId);

            if (repoKey == null)
                repoKey = new BlocksLanguageKey { ItemId = Guid.NewGuid().ToString(), CreateDate = DateTime.UtcNow, TenantId = _tenantId };

            repoKey.LastUpdateDate = DateTime.UtcNow;
            repoKey.KeyName = key.KeyName;
            //repoKey.Value = key.Value;
            repoKey.Resources = key.Resources;
            repoKey.IsPartiallyTranslated = key.IsPartiallyTranslated;

            return repoKey;
        }

        public async Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query)
        {
            return await _keyRepository.GetAllKeysAsync(query);
        }


        public async Task<bool> GenerateAsync(GenerateUilmFilesEvent command)
        {
            _logger.LogInformation("++ Started JsonOutputGeneratorService: GenerateAsync()...");

            List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();

            List<BlocksLanguageModule> applications = await _moduleManagementService.GetModulesAsync();

            _logger.LogInformation("++ JsonOutputGeneratorService: GenerateAsync()... Found {ApplicationsCount} UilmApplications.", applications.Count);

            foreach (BlocksLanguageModule application in applications)
            {
                List<Key> resourceKeys = await _keyRepository.GetAllKeysByModuleAsync(application.ItemId);
                _logger.LogInformation("++ JsonOutputGeneratorService: GenerateAsync()... Found {ResourceKeysCount} UilmResourceKeys for UilmApplication={ApplicationName}.", resourceKeys.Count, application.ModuleName);

                List<UilmFile> uilmfiles = ProcessUilmFile(command, languageSetting, resourceKeys, application);

                _logger.LogInformation("++Saving {UilmfilesCount} UilmFiles for UilmApplication={ApplicationName}", uilmfiles.Count, application.ModuleName);
                await SaveUniqeFiles(uilmfiles);
            };

            _logger.LogInformation("++JsonOutputGeneratorService: GenerateAsync execution successful!");

            return true;
        }

        public List<UilmFile> ProcessUilmFile(GenerateUilmFilesEvent command, List<Language> languages, List<Key> resourceKeys, BlocksLanguageModule application)
        {

            List<UilmFile> uilmfiles = new List<UilmFile>();
            foreach (Language language in languages)
            {
                Dictionary<string, object> dictionary = new Dictionary<string, object>();

                AssignResourceKeysToDictionary(resourceKeys, language, dictionary);

                UilmFile uilmFile = new UilmFile()
                {
                    Id = Guid.NewGuid().ToString(),
                    Language = language.LanguageCode,
                    ModuleName = application.ModuleName,
                    Content = JsonConvert.SerializeObject(dictionary)
                };

                uilmfiles.Add(uilmFile);
            }
            return uilmfiles;
        }

        private void AssignResourceKeysToDictionary(
           List<Key> resourceKeys,
           Language language,
           Dictionary<string, object> dictionary)
        {
            resourceKeys.ForEach((Key reosurceKey) =>
            {
                Resource resource = reosurceKey.Resources.FirstOrDefault(reosurce => reosurce.Culture == language.LanguageCode);

                string resourceValue = resource == null ? "[ KEY MISSING ]" : resource.Value;

                AssignToDictionary(dictionary: dictionary, keyPath: reosurceKey.KeyName, value: resourceValue);
            });
        }

        private void AssignToDictionary(
            Dictionary<string, object> dictionary,
            string keyPath,
            string value)
        {
            try
            {
                string[] keys = keyPath.Split('.');

                Dictionary<string, object> current = dictionary;

                for (int i = 0; i < keys.Length - 1; i++)
                {
                    if (current.ContainsKey(keys[i]))
                    {
                        current = (Dictionary<string, object>)current[keys[i]];
                    }
                    else
                    {
                        Dictionary<string, object> next = new Dictionary<string, object>();
                        current[keys[i]] = next;
                        current = next;
                    }
                }

                current[keys[keys.Length - 1]] = value;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in AssignToDictionary, keyPath: {keyPath},  exception: {ex}", keyPath, JsonConvert.SerializeObject(ex));
            }
        }

        public async Task<bool> SaveUniqeFiles(List<UilmFile> uilmfiles)
        {
            await _keyRepository.DeleteOldUilmFiles(uilmfiles);
            await _keyRepository.SaveNewUilmFiles(uilmfiles);
            return true;
        }

        public async Task<string> GetUilmFile(GetUilmFileRequest request)
        {
            var uilmFile =  await _keyRepository.GetUilmFile(request);
            return uilmFile?.Content;
        }
    }
}
