using Blocks.Genesis;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DomainService.Repositories;
using DomainService.Services.HelperService;
using DomainService.Shared;
using DomainService.Shared.Entities;
using DomainService.Shared.Events;
using DomainService.Shared.Utilities;
using DomainService.Storage;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using StorageDriver;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace DomainService.Services
{
    public class KeyManagementService : IKeyManagementService
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IKeyTimelineRepository _keyTimelineRepository;
        private readonly IValidator<Key> _validator;
        private readonly ILogger<KeyManagementService> _logger;
        private readonly ILanguageManagementService _languageManagementService;
        private readonly IModuleManagementService _moduleManagementService;
        private readonly IMessageClient _messageClient;
        private readonly IAssistantService _assistantService;
        private readonly IStorageDriverService _storageDriverService;
        private readonly IServiceProvider _serviceProvider;
        private readonly StorageHelper _storageHelperService;
        private readonly INotificationService _notificationService;

        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";
        private BaseBlocksCommand _blocksBaseCommand;
        private string _format;

        public KeyManagementService(
            IKeyRepository keyRepository,
            IKeyTimelineRepository keyTimelineRepository,
            IValidator<Key> validator,
            ILogger<KeyManagementService> logger,
            ILanguageManagementService languageManagementService,
            IModuleManagementService moduleManagementService,
            IMessageClient messageClient,
            IAssistantService assistantService,
            IStorageDriverService storageDriverService,
            StorageHelper storageHelperService,
            IServiceProvider serviceProvider,
            INotificationService notificationService
            )
        {
            _keyRepository = keyRepository;
            _keyTimelineRepository = keyTimelineRepository;
            _validator = validator;
            _logger = logger;
            _languageManagementService = languageManagementService;
            _moduleManagementService = moduleManagementService;
            _messageClient = messageClient;
            _assistantService = assistantService;
            _storageDriverService = storageDriverService;
            _storageHelperService = storageHelperService;
            _serviceProvider = serviceProvider;
            _notificationService = notificationService;
        }

        public async Task<ApiResponse> SaveKeyAsync(Key key)
        {
            var validationResult = await _validator.ValidateAsync(key);

            if (!validationResult.IsValid)
                return new ApiResponse(string.Empty, validationResult.Errors);

            try
            {
                // Get existing key for timeline tracking
                var existingRepoKey = await _keyRepository.GetKeyByNameAsync(key.KeyName, key.ModuleId);
                BlocksLanguageKey? previousKey = null;
                bool isNewKey = existingRepoKey == null;
                
                if (!isNewKey && existingRepoKey != null)
                {
                    previousKey = existingRepoKey;
                }

                var repoKey = await MappedIntoRepoKeyAsync(key);
                await _keyRepository.SaveKeyAsync(repoKey);
                if (key != null && key.ShouldPublish == true)
                {
                    var request = new GenerateUilmFilesRequest
                    {
                        Guid = key.ItemId,
                        ModuleId = key.ModuleId,
                        ProjectKey = key.ProjectKey
                    };
                    await SendGenerateUilmFilesEvent(request);
                }

                // Create timeline entry
                if (repoKey != null)
                {
                    if (isNewKey)
                    {
                        await CreateKeyTimelineEntryAsync(null, repoKey, "KeyController.Create");
                    }
                    else
                    {
                        await CreateKeyTimelineEntryAsync(previousKey, repoKey, "KeyController.Save");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while saving BlocksLanguage {errorMessage} : {StackTrace}", ex.Message, ex.StackTrace);
                return new ApiResponse(ex.Message);
            }

            return new ApiResponse();
        }

        public async Task<ApiResponse> SaveKeysAsync(List<Key> keys)
        {
            if (keys == null || !keys.Any())
            {
                return new ApiResponse("Keys list cannot be null or empty.");
            }

            var errors = new List<string>();
            var successCount = 0;

            foreach (var key in keys)
            {
                try
                {
                    var validationResult = await _validator.ValidateAsync(key);

                    if (!validationResult.IsValid)
                    {
                        var validationErrors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
                        errors.Add($"Key '{key.KeyName}' in Module '{key.ModuleId}': {validationErrors}");
                        continue;
                    }

                    // Get existing key for timeline tracking
                    var existingRepoKey = await _keyRepository.GetKeyByNameAsync(key.KeyName, key.ModuleId);
                    BlocksLanguageKey? previousKey = null;
                    bool isNewKey = existingRepoKey == null;
                    
                    if (!isNewKey && existingRepoKey != null)
                    {
                        previousKey = existingRepoKey;
                    }

                    var repoKey = await MappedIntoRepoKeyAsync(key);
                    await _keyRepository.SaveKeyAsync(repoKey);

                    if (key.ShouldPublish == true)
                    {
                        var request = new GenerateUilmFilesRequest
                        {
                            Guid = key.ItemId,
                            ModuleId = key.ModuleId,
                            ProjectKey = key.ProjectKey
                        };
                        await SendGenerateUilmFilesEvent(request);
                    }

                    // Create timeline entry
                    if (repoKey != null)
                    {
                        if (isNewKey)
                        {
                            await CreateKeyTimelineEntryAsync(null, repoKey, "KeyController.BulkCreate");
                        }
                        else
                        {
                            await CreateKeyTimelineEntryAsync(previousKey, repoKey, "KeyController.BulkSave");
                        }
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error while saving Key '{KeyName}' in Module '{ModuleId}': {ErrorMessage}", key.KeyName, key.ModuleId, ex.Message);
                    errors.Add($"Key '{key.KeyName}' in Module '{key.ModuleId}': {ex.Message}");
                }
            }

            _logger.LogInformation("Bulk save completed. Success: {SuccessCount}, Errors: {ErrorCount}", successCount, errors.Count);

            if (errors.Any())
            {
                var errorMessage = $"Bulk save completed with {errors.Count} errors out of {keys.Count} keys:\n" + string.Join("\n", errors);
                return new ApiResponse(errorMessage);
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
            repoKey.ModuleId = key.ModuleId;
            repoKey.Resources = key.Resources;
            repoKey.IsPartiallyTranslated = key.IsPartiallyTranslated;
            repoKey.Routes = key.Routes;
            repoKey.Context = key.Context;

            return repoKey;
        }

        public async Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query)
        {
            return await _keyRepository.GetAllKeysAsync(query);
        }

        public async Task<GetUilmExportedFilesQueryResponse> GetUilmExportedFilesAsync(GetUilmExportedFilesRequest request)
        {
            return await _keyRepository.GetUilmExportedFilesAsync(request);
        }

        public async Task<GetKeyTimelineQueryResponse> GetKeyTimelineAsync(GetKeyTimelineRequest query)
        {
            return await _keyTimelineRepository.GetKeyTimelineAsync(query);
        }

        public async Task<Key?> GetAsync(GetKeyRequest request)
        {
            var key = await _keyRepository.GetByIdAsync(request.ItemId);
            return key;
        }

        public async Task<BaseMutationResponse> DeleteAsysnc(DeleteKeyRequest request)
        {
            _logger.LogInformation("Deleting Key start");

            var key = await _keyRepository.GetByIdAsync(request.ItemId);
            if (key == null)
            {
                _logger.LogInformation("Deleting Key end -- Key not found");

                return new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "ItemId", "Key not found" }
                    }
                };
            }

            // Create timeline entry before deletion
            try
            {
                // Get the repository key for timeline
                var repoKey = await _keyRepository.GetKeyByNameAsync(key.KeyName, key.ModuleId);
                if (repoKey != null)
                {
                    await CreateKeyTimelineEntryAsync(repoKey, repoKey, "KeyController.Delete");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create timeline entry for deleted Key {KeyId}: {Error}", key.ItemId, ex.Message);
            }

            await _keyRepository.DeleteAsync(request.ItemId);

            _logger.LogInformation("Deleting Key end -- Success");
            return new BaseMutationResponse { IsSuccess = true };
        }

        public async Task<bool> ChangeAll(TranslateAllEvent request)
        {
            List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();

            var page = 0;
            var pageSize = 1000;

            while (true)
            {

                IQueryable<BlocksLanguageKey> dbResourceKeys = await _keyRepository.GetUilmResourceKeysWithPage(page, pageSize);

                if (!dbResourceKeys.Any())
                {
                    break;
                }

                // Create deep copies of original keys for timeline tracking
                var originalResourceKeys = new Dictionary<string, BlocksLanguageKey>();
                foreach (var key in dbResourceKeys)
                {
                    var originalKey = JsonConvert.DeserializeObject<BlocksLanguageKey>(JsonConvert.SerializeObject(key));
                    if (originalKey != null)
                    {
                        originalResourceKeys[key.ItemId] = originalKey;
                    }
                }

                var resourceKeys = await ProcessChangeAll(request, dbResourceKeys, languageSetting);

                if (resourceKeys.Any())
                {
                    await UpdateResourceKey(resourceKeys, request, originalResourceKeys);
                }

                page++;
            }

            return true;
        }

        public async Task<bool> TranslateBlocksLanguageKey(TranslateBlocksLanguageKeyEvent request)
        {
            try
            {
                List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();

                // Get the specific key by ID
                var resourceKey = await _keyRepository.GetUilmResourceKey(
                    x => x.ItemId == request.KeyId, 
                    BlocksContext.GetContext()?.TenantId ?? ""
                );

                if (resourceKey == null)
                {
                    _logger.LogWarning("TranslateBlocksLanguageKey: Key with ID {KeyId} not found", request.KeyId);
                    return false;
                }

                // Create deep copy for timeline tracking
                var originalKey = JsonConvert.DeserializeObject<BlocksLanguageKey>(JsonConvert.SerializeObject(resourceKey));
                
                var uilmResourceKeyList = new List<BlocksLanguageKey>();

                // Convert event to TranslateAllEvent format for reusing existing logic
                var translateAllEvent = new TranslateAllEvent
                {
                    MessageCoRelationId = request.MessageCoRelationId,
                    ProjectKey = request.ProjectKey,
                    DefaultLanguage = request.DefaultLanguage,
                    ModuleId = resourceKey.ModuleId // Use the ModuleId from the retrieved key
                };

                await ProcessResourceKey(translateAllEvent, resourceKey, languageSetting, uilmResourceKeyList);

                if (uilmResourceKeyList.Any())
                {
                    var originalResourceKeys = new Dictionary<string, BlocksLanguageKey>();
                    if (originalKey != null)
                    {
                        originalResourceKeys[resourceKey.ItemId] = originalKey;
                    }
                    
                    await UpdateResourceKey(uilmResourceKeyList, translateAllEvent, originalResourceKeys);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while translating BlocksLanguageKey with ID {KeyId}", request.KeyId);
                return false;
            }
        }

        public async Task<List<BlocksLanguageKey>> ProcessChangeAll(TranslateAllEvent request, IQueryable<BlocksLanguageKey> dbResourceKeys, List<Language> languageSetting)
        {
            var uilmResourceKeyList = new List<BlocksLanguageKey>();
            foreach (var resourceKey in dbResourceKeys)
            {
                await ProcessResourceKey(request, resourceKey, languageSetting, uilmResourceKeyList);
            }
            return uilmResourceKeyList;
        }

        public async Task ProcessResourceKey(TranslateAllEvent request, BlocksLanguageKey resourceKey, List<Language> languageSetting, List<BlocksLanguageKey> uilmResourceKeyList)
        {
            var keyName = resourceKey.KeyName;
            var resources = resourceKey.Resources?.ToList();

            EmptyResourcesThatHasReservedKeywords(uilmResourceKeyList, resourceKey, resources, request.DefaultLanguage);

            var defaultResource = resources?.FirstOrDefault(x => x.Culture == request.DefaultLanguage);

            //if (ShouldSkipResource(defaultResource, keyName, request))
            //{
            //    return;
            //}

            List<Resource> missingResources = GetMissingResources(keyName, resources, defaultResource, request.DefaultLanguage);

            CompareAndAddResources(missingResources, resources, languageSetting);

            if (missingResources.Any())
            {
                //var timeline = MakeBlocksLanguageManagerTimeline(command, resourceKey);

                foreach (var missingResource in missingResources)
                {
                    await ProcessMissingResource(request, resourceKey, defaultResource, missingResource, resources, languageSetting);

                }

                resourceKey.Resources = resources.ToArray();
                resourceKey.LastUpdateDate = DateTime.Now;
                resourceKey.ItemId = string.IsNullOrWhiteSpace(resourceKey.ItemId) ? Guid.NewGuid().ToString() : resourceKey.ItemId;

                uilmResourceKeyList.Add(resourceKey);

                //timeline.CurrentData = new LanguageManagerDto
                //{
                //    UilmResourceKey = resourceKey
                //};

                //blocksLanguageManagerTimelines.Add(timeline);
            }
        }

        public static bool ShouldSkipResource(Resource defaultResource, string keyName, TranslateAllEvent request)
        {
            return string.IsNullOrEmpty(defaultResource?.Value) || (defaultResource?.Value == keyName);
        }

        public static List<Resource> GetMissingResources(string keyName, List<Resource> resources, Resource defaultResource, string defaultLanguage)
        {
            return resources.Where(x => x.Culture != defaultLanguage && (x.Value == "" || x.Value == null)).ToList();
        }

        public void CompareAndAddResources(List<Resource> missingResources, IEnumerable<Resource> resources,
            List<Language> languageSetting)
        {
            var languageCodes = languageSetting.Select(x => x.LanguageCode).ToList();
            var resourceCultures = resources.Select(x => x.Culture).ToList();

            var missingCultures = languageCodes.Except(resourceCultures).ToList();

            if (missingCultures.Any())
            {
                foreach (var missingCulture in missingCultures)
                {
                    missingResources.Add(new Resource
                    {
                        Culture = missingCulture
                    });
                }
            }
        }

        public async Task ProcessMissingResource(TranslateAllEvent request, BlocksLanguageKey resourceKey, Resource defaultResource, Resource missingResource, List<Resource> resources, List<Language> languageSetting)
        {
            var languageName = languageSetting?.FirstOrDefault(x => x.LanguageCode == missingResource.Culture)?.LanguageName;

            if (string.IsNullOrEmpty(languageName))
            {
                _logger.LogError("ChangeAll: No language name found for languageCode {misssingResourceCulture}", missingResource.Culture);
                return;
            }

            missingResource.Value = await _assistantService.SuggestTranslation(ConstructQuery(request, resourceKey, defaultResource, missingResource, languageName, languageSetting));

            var matchedResource = resources.FirstOrDefault(x => x.Culture == missingResource.Culture);
            if (matchedResource != null)
            {
                resources.Remove(matchedResource);
            }
            resources.Add(missingResource);
        }

        public static SuggestLanguageRequest ConstructQuery(TranslateAllEvent request, BlocksLanguageKey resourceKey,
            Resource defaultResource, Resource missingResource, string languageName, List<Language> languageSetting)
        {
            return new()
            {
                //ElementType = resourceKey.Type,
                //Temperature = (resourceKey.Temperature / 100),
                Temperature = 0.1,
                //MaxCharacterLength = missingResource.CharacterLength,
                //ElementApplicationContext = command.ElementApplicationContext,
                ElementDetailContext = resourceKey.Context,
                SourceText = defaultResource?.Value,
                DestinationLanguage = languageName,
                CurrentLanguage = languageSetting?.FirstOrDefault(x => x.LanguageCode == request.DefaultLanguage).LanguageName
            };
        }

        public static void EmptyResourcesThatHasReservedKeywords(List<BlocksLanguageKey> missingResourceKeyResponseList, BlocksLanguageKey resourceKey, List<Resource> resources, string defaultLanguage)
        {
            if (HasKeywordValue(resources, defaultLanguage))
            {
                foreach (var item in resources)
                {
                    item.Value = "";
                }

                missingResourceKeyResponseList.Add(resourceKey);
            }
        }

        public static bool HasKeywordValue(List<Resource> resources, string defaultLanguage)
        {
            var keywordResources = resources.FirstOrDefault(x => x.Culture == defaultLanguage && x.Value?.ToUpper() == "KEY_MISSING");

            return keywordResources != null;
        }

        public async Task UpdateResourceKey(List<BlocksLanguageKey> resourceKeys, TranslateAllEvent request, Dictionary<string, BlocksLanguageKey>? originalResourceKeys = null)
        {
            var updateCount = await _keyRepository.UpdateUilmResourceKeysForChangeAll(resourceKeys);

            // Create timeline entries for updated keys
            foreach (var resourceKey in resourceKeys)
            {
                try
                {
                    // Use the original key for timeline comparison if available
                    BlocksLanguageKey? previousKey = null;
                    if (originalResourceKeys != null && originalResourceKeys.ContainsKey(resourceKey.ItemId))
                    {
                        previousKey = originalResourceKeys[resourceKey.ItemId];
                    }
                    
                    await CreateKeyTimelineEntryAsync(previousKey, resourceKey, "TranslateAll");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to create timeline entry for Key {KeyId} during TranslateAll: {Error}", resourceKey.ItemId, ex.Message);
                }
            }

            _logger.LogInformation($"ChangeAll: Uilm Resource key updated: {updateCount}");
        }

        public async Task<bool> GenerateAsync(GenerateUilmFilesEvent command)
        {
            _logger.LogInformation("++ Started JsonOutputGeneratorService: GenerateAsync()...");

            List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();

            List<BlocksLanguageModule> applications = string.IsNullOrWhiteSpace(command.ModuleId)
                ? await _moduleManagementService.GetModulesAsync()
                : await _moduleManagementService.GetModulesAsync(command.ModuleId);

            _logger.LogInformation("++ JsonOutputGeneratorService: GenerateAsync()... Found {ApplicationsCount} UilmApplications.", applications.Count);

            foreach (BlocksLanguageModule application in applications)
            {
                List<Key> resourceKeys = await _keyRepository.GetAllKeysByModuleAsync(application.ItemId);
                _logger.LogInformation("++ JsonOutputGeneratorService: GenerateAsync()... Found {ResourceKeysCount} UilmResourceKeys for UilmApplication={ApplicationName}.", resourceKeys.Count, application.ModuleName);

                List<UilmFile> uilmfiles = ProcessUilmFile(command, languageSetting, resourceKeys, application);

                _logger.LogInformation("++Saving {UilmfilesCount} UilmFiles for UilmApplication={ApplicationName}", uilmfiles.Count, application.ModuleName);
                await SaveUniqeFiles(uilmfiles);
            }
            ;

            _logger.LogInformation("++JsonOutputGeneratorService: GenerateAsync execution successful!");

            if(!string.IsNullOrWhiteSpace(command.ModuleId))
            {
                await _notificationService.NotifyExtensionEvent(true, command.ProjectKey);
            }

            return true;
        }

        public List<UilmFile> ProcessUilmFile(GenerateUilmFilesEvent command, List<Language> languages, List<Key> resourceKeys, BlocksLanguageModule application)
        {
            var keyLang = new Language
            {
                LanguageName = "key",
                LanguageCode = "key",
            };
            if (languages.FindIndex(x => x.LanguageName == "key") == -1)
            {
                languages.Add(keyLang);
            }
            List<UilmFile> uilmfiles = new List<UilmFile>();
            foreach (Language language in languages)
            {
                Dictionary<string, object> dictionary = new Dictionary<string, object>();
                if (language.LanguageCode == "key")
                {
                    AssignResourceKeysToDictionaryForKeyMode(resourceKeys, keyLang, dictionary);
                }
                else
                {
                    AssignResourceKeysToDictionary(resourceKeys, language, dictionary);
                }
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

                dictionary[reosurceKey.KeyName] = resourceValue;
            });
        }
        private void AssignResourceKeysToDictionaryForKeyMode(
           List<Key> resourceKeys,
           Language language,
           Dictionary<string, object> dictionary)
        {
            resourceKeys.ForEach((Key resourceKey) =>
            {
                AssignToDictionary(dictionary: dictionary, keyPath: resourceKey.KeyName, value: resourceKey.KeyName);
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
            var uilmFile = await _keyRepository.GetUilmFile(request);
            return uilmFile?.Content;
        }

        public async Task SendTranslateAllEvent(TranslateAllRequest request)
        {
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<TranslateAllEvent>
                {
                    ConsumerName = Utilities.Constants.TranslateAllKeysQueue,
                    Payload = new TranslateAllEvent
                    {
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey,
                        DefaultLanguage = request.DefaultLanguage
                    }
                }
            );
        }

        public async Task SendTranslateBlocksLanguageKeyEvent(TranslateBlocksLanguageKeyRequest request)
        {
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<TranslateBlocksLanguageKeyEvent>
                {
                    ConsumerName = Utilities.Constants.TranslateBlocksLanguageKeyQueue,
                    Payload = new TranslateBlocksLanguageKeyEvent
                    {
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey,
                        DefaultLanguage = request.DefaultLanguage,
                        KeyId = request.KeyId
                    }
                }
            );
        }

        public async Task SendUilmImportEvent(UilmImportRequest request)
        {
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<UilmImportEvent>
                {
                    ConsumerName = Utilities.Constants.UilmImportExportQueue,
                    Payload = new UilmImportEvent
                    {
                        FileId = request.FileId,
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey
                    }
                }
            );
        }

        public async Task SendUilmExportEvent(UilmExportRequest request)
        {
            var exportFileId = Guid.NewGuid().ToString();

            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<UilmExportEvent>
                {
                    ConsumerName = Utilities.Constants.UilmImportExportQueue,
                    Payload = new UilmExportEvent
                    {
                        FileId = exportFileId,
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey,
                        AppIds = request.AppIds,
                        CallerTenantId = request.CallerTenantId,
                        EndDate = request.EndDate,
                        StartDate = request.StartDate,
                        Languages = request.Languages,
                        OutputType = request.OutputType,
                        ReferenceFileId = request.ReferenceFileId
                    }
                }
            );
        }

        public async Task SendGenerateUilmFilesEvent(GenerateUilmFilesRequest request)
        {
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<GenerateUilmFilesEvent>
                {
                    ConsumerName = Utilities.Constants.UilmQueue,
                    Payload = new GenerateUilmFilesEvent
                    {
                        Guid = request.Guid,
                        ProjectKey = request.ProjectKey,
                        ModuleId = request.ModuleId
                    }
                }
            );
        }

        public async Task<bool> ImportUilmFile(UilmImportEvent request)
        {
            _logger.LogInformation("Importing Uilm file with ID: {FileId}", request.FileId);
            var (fileData, stream) = await GetFileStream(request.FileId, request.ProjectKey);
            if (fileData == null)
            {
                _logger.LogError("Uilm file with ID {FileId} not found", request.FileId);
                return false;
            }
            if (fileData.Name.EndsWith(".xlsx"))
            {
                _format = "XLSX";
                return await ImportExcelFile(stream, fileData);
            }
            else if (fileData.Name.EndsWith(".json"))
            {
                _format = "JSON";
                return await ImportJsonFile(stream, fileData);
            }
            else if (fileData.Name.EndsWith(".csv"))
            {
                _format = "CSV";
                return await ImportCsvFile(stream, fileData);
            }
            else if (fileData.Name.EndsWith(".xlf"))
            {
                _format = "XLF";
                return await ImportXlfFile(stream, fileData);
            }

            return false;
        }

        private async Task<bool> ImportCsvFile(Stream stream, FileResponse fileData)
        {
            try
            {
                var languageJsonModels = ExtractModelsFromCsv(stream);
                var dbApplications = await GetLanguageApplications(null);
                await ProcessJsonFile(dbApplications, languageJsonModels);

                _logger.LogInformation("ImportCsvFile: Successfully imported FileId:{id}, FileName: {name}", fileData.ItemId, fileData.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ImportCsvFile: Failed to import FileId:{id}, FileName: {name}, Error: {ex}", fileData.ItemId, fileData.Name, ex);
                return false;
            }
        }

        private static List<LanguageJsonModel> ExtractModelsFromCsv(Stream stream)
        {
            var memoryStream = stream as MemoryStream;
            var dataStream = new MemoryStream();
            dataStream.Write(memoryStream.ToArray(), 0, (memoryStream.ToArray()).Length);
            dataStream.Seek(0, SeekOrigin.Begin);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true
            };

            using (var reader = new StreamReader(dataStream))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();

                    var firstRow = csv.Parser.RawRecord;
                    var fields = firstRow.Split(',');

                    var cultures = new Dictionary<string, string?>();

                    // First, identify all culture columns (non-character length columns)
                    var cultureColumns = new List<string>();
                    for (int i = 4; i < fields.Length; i++) // Start from index 4 (after KeyName)
                    {
                        var fieldName = fields[i].Trim();
                        if (!fieldName.Contains("_CharacterLength"))
                        {
                            cultureColumns.Add(fieldName);
                        }
                    }

                    // Then map each culture to its corresponding character length column
                    foreach (var culture in cultureColumns)
                    {
                        string? characterLengthColumn = null;
                        var expectedCharLengthColumn = $"{culture}_CharacterLength";
                        
                        // Look for the character length column
                        for (int i = 4; i < fields.Length; i++)
                        {
                            if (fields[i].Trim().Equals(expectedCharLengthColumn, StringComparison.OrdinalIgnoreCase))
                            {
                                characterLengthColumn = expectedCharLengthColumn;
                                break;
                            }
                        }

                        cultures.Add(culture, characterLengthColumn);
                    }

                    var languageJsonModels = new List<LanguageJsonModel>();

                    while (csv.Read())
                    {
                        // Helper method to safely get optional fields
                        bool TryGetField<T>(string fieldName, out T value)
                        {
                            try
                            {
                                if (csv.TryGetField<T>(fieldName, out value))
                                {
                                    return true;
                                }
                            }
                            catch
                            {
                                // Field doesn't exist or conversion failed
                            }
                            value = default(T);
                            return false;
                        }

                        var languageJsonModel = new LanguageJsonModel
                        {
                            _id = csv.GetField<string>("ItemId"),
                            Module = csv.GetField<string>("Module"),
                            KeyName = csv.GetField<string>("KeyName"),
                            // Resources will be populated from individual culture columns below
                            ModuleId = csv.GetField<string>("ModuleId"),
                            IsPartiallyTranslated = TryGetField<bool>("IsPartiallyTranslated", out bool isPartiallyTranslated) ? isPartiallyTranslated : false,
                            //Routes = csv.GetField<string>("Routes")
                        };

                        var resources = new List<Resource>();

                        foreach (var culture in cultures)
                        {
                            var resource = new Resource();
                            resource.Culture = culture.Key;
                            resource.Value = csv.GetField<string>(culture.Key);
                            resource.CharacterLength = string.IsNullOrEmpty(culture.Value) ? 0 : csv.GetField<int>(culture.Value);

                            resources.Add(resource);
                        }

                        languageJsonModel.Resources = resources.ToArray();

                        languageJsonModels.Add(languageJsonModel);
                    }

                    return languageJsonModels;
                }
            }
        }

        private async Task<bool> ImportJsonFile(Stream stream, FileResponse fileData)
        {
            try
            {
                var languageJsonModels = ExtractModelsFromJson(stream);
                var dbApplications = await GetLanguageApplications(null);
                await ProcessJsonFile(dbApplications, languageJsonModels);

                _logger.LogInformation("ImportJsonFile: Successfully imported FileId:{id}, FileName: {name}", fileData.ItemId, fileData.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ImportJsonFile: Failed to import FileId:{id}, FileName: {name}, Error: {ex}", fileData.ItemId, fileData.Name, ex);
                return false;
            }
        }

        private async Task ProcessJsonFile(List<BlocksLanguageModule> dbApplications, List<LanguageJsonModel> languageJsonModels)
        {
            var uilmApplicationsToBeInserted = new List<BlocksLanguageModule>();
            var uilmApplicationsToBeUpdated = new List<BlocksLanguageModule>();

            var resourceKeysWithoutId = new List<BlocksLanguageKey>();
            var uilmResourceKeys = new List<BlocksLanguageKey>();
            var oldUilmResourceKeys = new List<BlocksLanguageKey>();

            // var uilmAppTimeLines = new List<BlocksLanguageManagerTimeline>();
            // var uilmResourceKeyTimeLines = new List<BlocksLanguageManagerTimeline>();

            foreach (var languageJsonModel in languageJsonModels)
            {
                var id = languageJsonModel._id;
                var appId = languageJsonModel.ModuleId;
                var isPartiallyTranslated = languageJsonModel.IsPartiallyTranslated;
                var moduleName = languageJsonModel?.Module;
                var keyName = languageJsonModel.KeyName;
                //var type = languageJsonModel.Type;


                //            var model = new LanguageJsonModel
                //            {
                //                _id = resourceKey.ItemId,
                //                ModuleId = resourceKey.ModuleId,
                //                Value = resourceKey.Value,
                //                KeyName = resourceKey.KeyName,
                //                Resources = resourceKey.Resources.Where(x => identifiers.Contains(x.Culture)).ToArray()
                //TenantId = resourceKey.TenantId,
                //                IsPartiallyTranslated = resourceKey.IsPartiallyTranslated,
                //                Routes = resourceKey.Routes
                //            };
                //var uilmAppTimeLine = GetBlocksLanguageManagerTimeline();

                appId = HandleUilmApplication(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, appId, isPartiallyTranslated,
                    moduleName);

                //uilmAppTimeLines.Add(uilmAppTimeLine);

                BlocksLanguageKey uilmResourceKey = new()
                {
                    KeyName = keyName,
                    Resources = languageJsonModel.Resources,
                    ItemId = id,
                    ModuleId = appId,
                    IsPartiallyTranslated = isPartiallyTranslated,
                    CreateDate = DateTime.UtcNow,
                    LastUpdateDate = DateTime.UtcNow,
                    Value = string.Empty, // Value field is not exported, set to empty
                    Routes = languageJsonModel.Routes
                };

                //var uilmResourceKeyTimeLine = GetBlocksLanguageManagerTimeline();
                var olduilmResourceKey = await GetUilmResourceKey(uilmResourceKey.ModuleId, uilmResourceKey.KeyName);

                uilmResourceKey.ItemId = string.IsNullOrWhiteSpace(uilmResourceKey.ItemId) ? Guid.NewGuid().ToString() : uilmResourceKey.ItemId;

                if (olduilmResourceKey == null)
                {
                    resourceKeysWithoutId.Add(uilmResourceKey);
                }
                else
                {
                    oldUilmResourceKeys.Add(olduilmResourceKey);
                    uilmResourceKeys.Add(uilmResourceKey);
                }
                //FormatUilmResouceKeyTimeline(uilmResourceKeyTimeLine, olduilmResourceKey, uilmResourceKey);
                //uilmResourceKeyTimeLines.Add(uilmResourceKeyTimeLine);
            }

            await SaveUilmResourceKey(uilmResourceKeys, resourceKeysWithoutId, oldUilmResourceKeys);

            var validUilmApplicationsToBeInserted = uilmApplicationsToBeInserted.Where(x => x != null && x.ModuleName != null).DistinctBy(x => x.ModuleName).ToList();
            var validUilmApplicationsToBeUpdated = uilmApplicationsToBeUpdated.Where(x => x != null && x.ModuleName != null).DistinctBy(x => x.ModuleName).ToList();
            await SaveUilmApplication(validUilmApplicationsToBeInserted, validUilmApplicationsToBeUpdated);

            // uilmResourceKeyTimeLines.AddRange(uilmAppTimeLines.Where(x => x?.CurrentData?.UilmApplication?.ModuleName != null).DistinctBy(x => x.CurrentData.UilmApplication.ModuleName).ToList());
            //await _uilmRepository.SaveBlocksLanguageManagerTimeLines(uilmResourceKeyTimeLines);
        }

        private async Task<List<BlocksLanguageModule>> GetLanguageApplications(List<string> appIds = null)
        {
            List<BlocksLanguageModule> applications = null;

            //if (_blocksBaseCommand?.IsExternal)
            //{
            //    if (appIds != null && appIds.Count > 0)
            //    {
            //        var blocksApplication = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => appIds.Contains(x.ItemId), null);
            //        applications = blocksApplication?.Select(x => (BlocksLanguageModule)x).ToList();
            //    }
            //    else
            //    {
            //        var blocksApplication = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => true, null);
            //        applications = blocksApplication?.Select(x => (BlocksLanguageModule)x).ToList();
            //    }
            //}
            //else
            //{
            if (appIds != null && appIds.Count > 0)
            {
                applications = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => appIds.Contains(x.ItemId));
            }
            else
            {
                applications = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => true );
            }
            //}

            return applications;
        }

        private static List<LanguageJsonModel> ExtractModelsFromJson(Stream stream)
        {
            List<LanguageJsonModel> languageJsonModels;
            var memoryStream = stream as MemoryStream;
            var dataStream = new MemoryStream();
            dataStream.Write(memoryStream.ToArray(), 0, (memoryStream.ToArray()).Length);
            dataStream.Seek(0, SeekOrigin.Begin);

            using (var file = new StreamReader(dataStream))
            {
                using (var reader = new JsonTextReader(file))
                {
                    var serializer = new JsonSerializer();
                    languageJsonModels = serializer.Deserialize<List<LanguageJsonModel>>(reader);
                }
            }

            return languageJsonModels;
        }

        private async Task<(FileResponse, Stream)> GetFileStream(string fileId, string projectKey)
        {

            var fileData = await _storageDriverService.GetUrlForDownloadFileAsync(new GetFileRequest
            {
                FileId = fileId,
                ProjectKey = projectKey
            });
            if (fileData is null)
            {
                _logger.LogError("ImportUilmFile: File data is null with the file Id: {id}", fileId);
                return (null, null);
            }

            var stream = await GetFileStream(fileData);
            if (stream is null)
            {
                _logger.LogError("ImportUilmFile: File stream is null with the file Id: {id}", fileId);
                return (null, null);
            }

            _logger.LogInformation("ImportUilmFile: Fetched FileContent for FileId={FileId} FileName={FileDataName}.", fileId, fileData.Name);

            return (fileData, stream);
        }

        private async Task<Stream> GetFileStream(FileResponse fileData)
        {

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Blocks-Key", BlocksContext.GetContext()?.TenantId);


            var fileUrl = fileData.Url;

            var response = await httpClient.GetAsync(fileUrl);



            if (!response.IsSuccessStatusCode)
            {
                return Stream.Null;
            }

            var memoryStream = new MemoryStream();

            await response.Content.CopyToAsync(memoryStream);

            return memoryStream;
        }

        private async Task<bool> ImportExcelFile(Stream stream, FileResponse fileData)
        {
            try
            {
                using XLWorkbook workbook = new XLWorkbook(stream);
                IXLWorksheet worksheet = workbook.Worksheets.First();
                worksheet.Columns().Unhide();

                // header value, column letter
                Dictionary<string, string> columns = new Dictionary<string, string>();
                Dictionary<string, string> languages = new Dictionary<string, string>();

                List<string> systemColumns = new List<string>() { "ItemId", "ModuleId", "Module", "KeyName" };
                List<BlocksLanguageKey> blocksLanguageKeys = new List<BlocksLanguageKey>();

                foreach (IXLColumn col in worksheet.Columns())
                {
                    string columnLetter = col.ColumnLetter();
                    string header = worksheet.Cell(1, columnLetter).Value.ToString();
                    if (!string.IsNullOrEmpty(header) && !columns.ContainsKey(header))
                    {
                        columns.Add(header.Trim(), columnLetter);
                    }

                    if (!string.IsNullOrEmpty(header) && !systemColumns.Contains(header) && !languages.ContainsKey(header))
                    {
                        languages.Add(header.Trim(), columnLetter);
                    }
                }

                if (columns.Count == 0)
                {
                    _logger.LogError("ImportExcelFile: No column found in the excel FileId: {id}, FileName: {name}", fileData.ItemId, fileData.Name);
                    return false;
                }

                _logger.LogInformation("ImportExcelFile: Detected {ColumnsCount} columns={Columns} in FileName={FileDataName}", columns.Count, string.Join(", ", columns.Select(x => x.Key).ToList()), fileData.Name);
                _logger.LogInformation("ImportExcelFile: Detected {LanguagesCount} cultures={Cultures} in FileName={FileDataName}", languages.Count, string.Join(", ", languages.Select(x => x.Key).ToList()), fileData.Name);

                // Validate required columns exist
                var requiredColumns = new[] { "ItemId", "ModuleId", "Module", "KeyName" };
                var missingColumns = requiredColumns.Where(col => !columns.ContainsKey(col)).ToList();
                if (missingColumns.Any())
                {
                    _logger.LogError("ImportExcelFile: Missing required columns {MissingColumns} in FileId: {id}, FileName: {name}", string.Join(", ", missingColumns), fileData.ItemId, fileData.Name);
                    return false;
                }

                await ProcessExcelCells(worksheet, columns, languages, blocksLanguageKeys);

                _logger.LogInformation("ImportExcelFile: Successfully imported FileId:{id}, FileName: {name}", fileData.ItemId, fileData.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ImportExcelFile: Failed to import FileId:{id}, FileName: {name}, Error: {ex}", fileData.ItemId, fileData.Name, ex);
                return false;
            }
        }

        private async Task<bool> ImportXlfFile(Stream stream, FileResponse fileData)
        {
            try
            {
                var languageJsonModels = ExtractModelsFromXlf(stream);
                var dbApplications = await GetLanguageApplications(null);
                await ProcessJsonFile(dbApplications, languageJsonModels);

                _logger.LogInformation("ImportXlfFile: Successfully imported FileId:{id}, FileName: {name}", fileData.ItemId, fileData.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ImportXlfFile: Failed to import FileId:{id}, FileName: {name}, Error: {ex}", fileData.ItemId, fileData.Name, ex);
                return false;
            }
        }

        private static List<LanguageJsonModel> ExtractModelsFromXlf(Stream stream)
        {
            var memoryStream = stream as MemoryStream;
            var dataStream = new MemoryStream();
            dataStream.Write(memoryStream.ToArray(), 0, memoryStream.ToArray().Length);
            dataStream.Seek(0, SeekOrigin.Begin);

            XNamespace ns = "urn:oasis:names:tc:xliff:document:1.2";
            var document = XDocument.Load(dataStream);

            var languageJsonModels = new Dictionary<string, LanguageJsonModel>();

            // Parse all <file> elements
            var fileElements = document.Root?.Elements(ns + "file");
            if (fileElements == null)
            {
                return new List<LanguageJsonModel>();
            }

            foreach (var fileElement in fileElements)
            {
                var sourceLanguage = fileElement.Attribute("source-language")?.Value;
                var targetLanguage = fileElement.Attribute("target-language")?.Value;
                var moduleName = fileElement.Attribute("original")?.Value;

                var body = fileElement.Element(ns + "body");
                if (body == null) continue;

                // Parse all <trans-unit> elements
                var transUnits = body.Elements(ns + "trans-unit");
                foreach (var transUnit in transUnits)
                {
                    var transUnitId = transUnit.Attribute("id")?.Value;
                    var keyName = transUnit.Attribute("resname")?.Value;

                    if (string.IsNullOrEmpty(keyName)) continue;

                    // Extract ItemId from trans-unit id (format: ItemId_Culture)
                    var itemId = transUnitId?.Split('_')[0];

                    var sourceElement = transUnit.Element(ns + "source");
                    var targetElement = transUnit.Element(ns + "target");
                    var noteElements = transUnit.Elements(ns + "note");

                    var sourceValue = sourceElement?.Value;
                    var targetValue = targetElement?.Value;
                    var targetState = targetElement?.Attribute("state")?.Value;

                    // Extract metadata from notes
                    string moduleId = null;
                    var routes = new List<string>();
                    int characterLength = 0;

                    foreach (var note in noteElements)
                    {
                        var noteValue = note.Value;
                        if (noteValue.StartsWith("Module:"))
                        {
                            // Module name is already in fileElement.Attribute("original")
                        }
                        else if (noteValue.StartsWith("Routes:"))
                        {
                            var routesStr = noteValue.Replace("Routes:", "").Trim();
                            routes = routesStr.Split(',').Select(r => r.Trim()).ToList();
                        }
                        else if (noteValue.StartsWith("CharacterLength:"))
                        {
                            var charLengthStr = noteValue.Replace("CharacterLength:", "").Trim();
                            int.TryParse(charLengthStr, out characterLength);
                        }
                    }

                    // Create or update LanguageJsonModel
                    if (!languageJsonModels.ContainsKey(keyName))
                    {
                        languageJsonModels[keyName] = new LanguageJsonModel
                        {
                            _id = itemId,
                            Module = moduleName,
                            KeyName = keyName,
                            Resources = new List<Resource>().ToArray(),
                            Routes = routes,
                            IsPartiallyTranslated = targetState == "needs-translation"
                        };
                    }

                    var model = languageJsonModels[keyName];

                    // Add or update resources
                    var resourceList = model.Resources?.ToList() ?? new List<Resource>();

                    // Add source language resource if not exists
                    if (!string.IsNullOrEmpty(sourceLanguage) && !string.IsNullOrEmpty(sourceValue))
                    {
                        var sourceResource = resourceList.FirstOrDefault(r => r.Culture == sourceLanguage);
                        if (sourceResource == null)
                        {
                            resourceList.Add(new Resource
                            {
                                Culture = sourceLanguage,
                                Value = sourceValue,
                                CharacterLength = 0
                            });
                        }
                    }

                    // Add target language resource
                    if (!string.IsNullOrEmpty(targetLanguage))
                    {
                        var targetResource = resourceList.FirstOrDefault(r => r.Culture == targetLanguage);
                        if (targetResource == null)
                        {
                            resourceList.Add(new Resource
                            {
                                Culture = targetLanguage,
                                Value = targetValue ?? string.Empty,
                                CharacterLength = characterLength
                            });
                        }
                        else
                        {
                            // Update existing target resource
                            targetResource.Value = targetValue ?? string.Empty;
                            targetResource.CharacterLength = characterLength;
                        }
                    }

                    model.Resources = resourceList.ToArray();
                }
            }

            return languageJsonModels.Values.ToList();
        }

        private async Task ProcessExcelCells(IXLWorksheet worksheet, Dictionary<string, string> columns, Dictionary<string, string> languages,
            List<BlocksLanguageKey> uilmResourceKeys)
        {
            //List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();
            List<BlocksLanguageKey> oldUilmResourceKeys = new List<BlocksLanguageKey>();

            List<BlocksLanguageModule> dbApplications = await _moduleManagementService.GetModulesAsync();

            var uilmApplicationsToBeInserted = new List<BlocksLanguageModule>();
            var uilmApplicationsToBeUpdated = new List<BlocksLanguageModule>();

            var resourceKeysWithoutId = new List<BlocksLanguageKey>();
            var cultures = languages.Where(x => !x.Key.Contains("_CharacterLength")).ToDictionary(x => x.Key, y => y.Value);

            var excelRows = worksheet.RowsUsed().Count();

            _logger.LogInformation("ImportExcelFile: {Excelrows} UilmResourceKeys Found!", excelRows - 1);

            //var uilmAppTimeLines = new List<BlocksLanguageManagerTimeline>();
            //var uilmResourceKeyTimeLines = new List<BlocksLanguageManagerTimeline>();

            for (int i = 2; i <= excelRows; i++)
            {
                string id = worksheet.Cell(i, columns["ItemId"]).Value.ToString();
                string moduleId = worksheet.Cell(i, columns["ModuleId"]).Value.ToString();
                string moduleName = worksheet.Cell(i, columns["Module"]).Value.ToString();
                string keyName = worksheet.Cell(i, columns["KeyName"]).Value.ToString();
                // Note: Resources column is not required as resources are populated from language columns
                //string moduleName = worksheet.Cell(i, columns["module"]).Value.ToString();
                //string type = worksheet.Cell(i, columns["type"]).Value.ToString();

                //var uilmAppTimeLine = GetBlocksLanguageManagerTimeline();

                //moduleId = HandleUilmApplication(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, moduleId, moduleName, moduleName);
                moduleId = HandleUilmApplication(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, moduleId, false, moduleName);

                //uilmAppTimeLines.Add(uilmAppTimeLine);

                BlocksLanguageKey uilmResourceKey = new()
                {
                    KeyName = keyName,
                    ItemId = id,
                    ModuleId = moduleId,
                    LastUpdateDate = DateTime.UtcNow,
                    CreateDate = DateTime.UtcNow
                };

                uilmResourceKey.Resources = new Resource[cultures.Count];

                int j = 0;
                foreach (KeyValuePair<string, string> lang in cultures)
                {
                    string resourceValue = worksheet.Cell(i, lang.Value).Value.ToString();
                    int characterLength = 0;

                    var key = lang.Key + "_CharacterLength";
                    //if (lang.Key != defaultLanguage && languages.ContainsKey(key))
                    //{
                    //    characterLength = AssignCharacterLengthValue(worksheet, languages, i, key);
                    //}

                    uilmResourceKey.Resources[j++] = (new Resource() { Culture = lang.Key, Value = resourceValue, CharacterLength = characterLength });
                }

                //var uilmResourceKeyTimeLine = GetBlocksLanguageManagerTimeline();

                var olduilmResourceKey = await GetUilmResourceKey(uilmResourceKey.ModuleId, uilmResourceKey.KeyName);

                uilmResourceKey.ItemId = string.IsNullOrWhiteSpace(uilmResourceKey.ItemId) ? Guid.NewGuid().ToString() : uilmResourceKey.ItemId;

                if (olduilmResourceKey == null)
                {
                    resourceKeysWithoutId.Add(uilmResourceKey);
                }
                else
                {
                    uilmResourceKey.ItemId = string.IsNullOrWhiteSpace(olduilmResourceKey.ItemId) ? uilmResourceKey.ItemId : olduilmResourceKey.ItemId;
                    oldUilmResourceKeys.Add(olduilmResourceKey);
                    uilmResourceKeys.Add(uilmResourceKey);
                }

                //FormatUilmResouceKeyTimeline(uilmResourceKeyTimeLine, olduilmResourceKey, uilmResourceKey);
                //uilmResourceKeyTimeLines.Add(uilmResourceKeyTimeLine);
            }

            await SaveUilmResourceKey(uilmResourceKeys, resourceKeysWithoutId, oldUilmResourceKeys);
            await SaveUilmApplication(uilmApplicationsToBeInserted.DistinctBy(x => x.ModuleName).ToList(), uilmApplicationsToBeUpdated.DistinctBy(x => x.ModuleName).ToList());

            //uilmResourceKeyTimeLines?.AddRange(uilmAppTimeLines?.DistinctBy(x => x.CurrentData?.UilmApplication?.ModuleName)?.ToList());
            //await _uilmRepository.SaveBlocksLanguageManagerTimeLines(uilmResourceKeyTimeLines);
        }

        private string HandleUilmApplication(List<BlocksLanguageModule> dbApplications, List<BlocksLanguageModule> uilmApplicationsToBeInserted,
            List<BlocksLanguageModule> uilmApplicationsToBeUpdated, string appId, bool isPartiallyTranslated, string moduleName)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = HandleApplicationWithoutAppId(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, isPartiallyTranslated, moduleName);
            }
            else
            {
                HandleApplicationWithAppId(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, appId, isPartiallyTranslated, moduleName);
            }

            return appId;
        }

        // private BlocksLanguageManagerTimeline GetBlocksLanguageManagerTimeline()
        // {
        //     return new BlocksLanguageManagerTimeline
        //     {
        //         ClientTenantId = _blocksBaseCommand?.ClientTenantId,
        //         ClientSiteId = _blocksBaseCommand?.ClientSiteId,
        //         OrganizationId = _blocksBaseCommand?.OrganizationId,
        //         UserId = BlocksContext.GetContext()?.UserId ?? ""
        //     };
        // }

        private string HandleApplicationWithoutAppId(List<BlocksLanguageModule> dbApplications, List<BlocksLanguageModule> uilmApplicationsToBeInserted,
            List<BlocksLanguageModule> uilmApplicationsToBeUpdated, bool isPartiallyTranslated, string moduleName)
        {
            string appId;
            var application = dbApplications?.FirstOrDefault(x => x.ModuleName == moduleName);
            if (application != null)
            {
                //uilmAppTimeLine.LogFrom = "IMPORT_UPDATE_" + _format;
                //uilmAppTimeLine.PreviousData = new LanguageManagerDto
                //{
                //    UilmApplication = JsonConvert.DeserializeObject<BlocksLanguageModule>(JsonConvert.SerializeObject(application)),
                //};

                appId = application.ItemId;
                //appId = application.Id;
                //application.Name = appName;
                application.ModuleName = moduleName;

                var alreadyAddedToUpdateList = uilmApplicationsToBeUpdated.FirstOrDefault(x => x.ModuleName == moduleName);
                if (alreadyAddedToUpdateList is null)
                {
                    uilmApplicationsToBeUpdated.Add(application);
                }

                //uilmAppTimeLine.CurrentData = new LanguageManagerDto
                //{
                //    UilmApplication = application,
                //};
            }
            else
            {
                var alreadyInsertedToApp = uilmApplicationsToBeInserted.FirstOrDefault(x => x.ModuleName == moduleName);
                if (alreadyInsertedToApp is null)
                {
                    var app = new BlocksLanguageModule()
                    {
                        ItemId = Guid.NewGuid().ToString(),
                        //Name = appName,
                        ModuleName = moduleName,
                    };

                    uilmApplicationsToBeInserted.Add(app);

                    //uilmAppTimeLine.PreviousData = null;
                    //uilmAppTimeLine.LogFrom = "IMPORT_ADD_" + _format;
                    //uilmAppTimeLine.CurrentData = new LanguageManagerDto
                    //{
                    //    UilmApplication = application,
                    //};

                    appId = app.ItemId;
                    //appId = app.Id;
                }
                else
                {
                    appId = alreadyInsertedToApp.ItemId;
                    //appId = alreadyInsertedToApp.Id;
                }
            }

            return appId;
        }

        private void HandleApplicationWithAppId(List<BlocksLanguageModule> dbApplications, List<BlocksLanguageModule> uilmApplicationsToBeInserted, List<BlocksLanguageModule> uilmApplicationsToBeUpdated,
            string appId, bool isPartiallyTranslated, string moduleName)
        {
            var application = dbApplications?.FirstOrDefault(x => x.ItemId == appId);
            if (application != null)
            {
                //uilmAppTimeLine.PreviousData = new LanguageManagerDto
                //{
                //    UilmApplication = JsonConvert.DeserializeObject<BlocksLanguageModule>(JsonConvert.SerializeObject(application)),
                //};
                //uilmAppTimeLine.LogFrom = "IMPORT_UPDATE_" + _format;

                //application.Name = appName;
                application.ModuleName = moduleName;

                var alreadyAddedToUpdateList = uilmApplicationsToBeUpdated.FirstOrDefault(x => x.ItemId == appId);
                if (alreadyAddedToUpdateList is null)
                {
                    uilmApplicationsToBeUpdated.Add(application);
                }

                //uilmAppTimeLine.CurrentData = new LanguageManagerDto
                //{
                //    UilmApplication = application,
                //};
            }
            else
            {
                var alreadyAddedToInsertList = uilmApplicationsToBeInserted.FirstOrDefault(x => x.ItemId == appId);
                if (alreadyAddedToInsertList is null)
                {
                    BlocksLanguageModule uilmApplication = new()
                    {
                        ItemId = appId,
                        //Name = appName,
                        ModuleName = moduleName,
                    };

                    uilmApplicationsToBeInserted.Add(uilmApplication);

                    //uilmAppTimeLine.PreviousData = null;
                    //uilmAppTimeLine.LogFrom = "IMPORT_ADD_" + _format;
                    //uilmAppTimeLine.CurrentData = new LanguageManagerDto
                    //{
                    //    UilmApplication = application,
                    //};
                }
            }
        }

        private async Task<BlocksLanguageKey> GetUilmResourceKey(string appId, string keyName)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(keyName)) return null;

            //if (_blocksBaseCommand?.IsExternal)
            //{
            //    return await _keyRepository.GetUilmResourceKey<BlocksLanguageKey>(x => x.ModuleId == appId && x.KeyName == keyName);
            //}

            return await _keyRepository.GetUilmResourceKey(x => x.ModuleId == appId && x.KeyName == keyName, _blocksBaseCommand?.ClientTenantId);
        }

        private async Task SaveUilmResourceKey(List<BlocksLanguageKey> uilmResourceKeys, List<BlocksLanguageKey> resourceKeysWithoutId, List<BlocksLanguageKey> oldUilmResourceKeys = null)
        {
            if (uilmResourceKeys.Any())
            {
                long? updateCount = 0;

                updateCount = await _keyRepository.UpdateUilmResourceKeysForChangeAll(uilmResourceKeys);

                // Create timeline entries for updated keys
                foreach (var resourceKey in uilmResourceKeys)
                {
                    try
                    {
                        await CreateKeyTimelineEntryAsync(oldUilmResourceKeys.FirstOrDefault(x => x.ItemId == resourceKey.ItemId), resourceKey, "UilmImport.Update");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to create timeline entry for updated Key {KeyId} during UilmImport: {Error}", resourceKey.ItemId, ex.Message);
                    }
                }

                _logger.LogInformation("SaveUilmResourceKey: Updated UilmResourceKeys:{count}", updateCount);
            }
            if (resourceKeysWithoutId.Any())
            {
                //if (!_blocksBaseCommand?.IsExternal)
                //{
                await _keyRepository.InsertUilmResourceKeys(resourceKeysWithoutId, _blocksBaseCommand?.ClientTenantId);
                //}

                // Create timeline entries for inserted keys
                foreach (var resourceKey in resourceKeysWithoutId)
                {
                    try
                    {
                        await CreateKeyTimelineEntryAsync(null, resourceKey, "UilmImport.Insert");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to create timeline entry for new Key {KeyId} during UilmImport: {Error}", resourceKey.ItemId, ex.Message);
                    }
                }

                _logger.LogInformation("SaveUilmResourceKey: Inserted UilmResourceKeys:{count}", resourceKeysWithoutId.Count);
            }
        }

        private BlocksLanguageKey GetBlocksLanguageKey(BlocksLanguageKey key)
        {
            return new BlocksLanguageKey
            {
                ModuleId = key.ModuleId,
                KeyName = key.KeyName,
                Resources = key.Resources,
                ItemId = key.ItemId,
                CreateDate = key.CreateDate,
                LastUpdateDate = key.LastUpdateDate,
            };
        }

        private async Task SaveUilmApplication(List<BlocksLanguageModule> uilmApplicationsToBeInserted,
            List<BlocksLanguageModule> uilmApplicationsToBeUpdated)
        {
            if (uilmApplicationsToBeUpdated.Any())
            {
                await _keyRepository.UpdateBulkUilmApplications(uilmApplicationsToBeUpdated, _blocksBaseCommand?.OrganizationId, _blocksBaseCommand?.IsExternal ?? false, _blocksBaseCommand?.ClientTenantId);

                await AddNumberOfKeysInUilmApplications(uilmApplicationsToBeUpdated);
            }

            if (uilmApplicationsToBeInserted.Any())
            {
                await InsertUilmApplications(uilmApplicationsToBeInserted);

                await AddNumberOfKeysInUilmApplications(uilmApplicationsToBeInserted);
            }
        }

        private async Task AddNumberOfKeysInUilmApplications(List<BlocksLanguageModule> uilmApplications)
        {
            foreach (var application in uilmApplications)
            {
                await _keyRepository.UpdateKeysCountOfAppAsync(application.ItemId, _blocksBaseCommand?.IsExternal ?? false, _blocksBaseCommand?.ClientTenantId, _blocksBaseCommand?.OrganizationId);
            }
        }

        private async Task InsertUilmApplications(List<BlocksLanguageModule> uilmApplicationsToBeInserted)
        {
            //if (!_blocksBaseCommand?.IsExternal)
            //{
            await _keyRepository.InsertUilmApplications(uilmApplicationsToBeInserted, _blocksBaseCommand?.ClientTenantId);
            //}

            //await _keyRepository.InsertUilmApplications(uilmApplicationsToBeInserted.Select(x => new BlocksLanguageModule
            //{
            //    ItemId = x.ItemId,
            //    Name = x.Name,
            //    ModuleName = x.ModuleName
            //}));
        }

        public async Task<bool> ExportUilmFile(UilmExportEvent request)
        {
            var languageSettings = await GetLanguageSetting();
            var languageApplications = await GetLanguageApplications(request.AppIds);
            var languageResourceKeys = await GetLanguageResourceKeys(request.AppIds, request.StartDate, request.EndDate);

            switch (request.OutputType)
            {
                case OutputType.Xlsx:
                    return await GenerateXlsxFile(languageApplications, languageResourceKeys, request.FileId, languageSettings, request.Languages);
                case OutputType.Json:
                    return await GenerateJsonFile(languageApplications, languageResourceKeys, request.FileId, languageSettings, request.Languages);
                case OutputType.Csv:
                    return await GenerateCsvFile(languageApplications, languageResourceKeys, request.FileId, languageSettings, request.Languages);
                case OutputType.Xlf:
                    return await GenerateXlfFile(languageApplications, languageResourceKeys, request.FileId, languageSettings, request.Languages, request.ReferenceFileId, request.ProjectKey);
                default:
                    return false;
            }
        }

        private async Task<BlocksLanguage> GetLanguageSetting()
        {
            BlocksLanguage languageSetting = null;

            languageSetting = await _keyRepository.GetLanguageSettingAsync(_blocksBaseCommand?.ClientTenantId);

            return languageSetting;
        }

        private async Task<bool> GenerateXlsxFile(List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting, List<string> requestedLanguages)
        {
            var xlsxOutputGenerator = _serviceProvider.GetService<XlsxOutputGeneratorService>();

            // Get all languages from BlocksLanguage collection
            var allLanguages = await _keyRepository.GetAllLanguagesAsync(string.Empty);

            // Filter languages if specific languages are requested
            if (requestedLanguages != null && requestedLanguages.Any())
            {
                allLanguages = allLanguages.Where(l => requestedLanguages.Contains(l.LanguageCode)).ToList();
            }

            var workBook = await xlsxOutputGenerator.GenerateAsync<XLWorkbook>(allLanguages, applications, resourceKeys, languageSetting.LanguageCode);
            if (workBook == null)
            {
                _logger.LogError("GenerateAndWriteFile: Workbook is null");
                return false;
            }
            var xlsxStream = new MemoryStream();
            workBook.SaveAs(xlsxStream);
            var fileName = "uilm_xlsx_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
            return await SaveUilmFile(fileId, fileName, xlsxStream);
        }

        private async Task<bool> SaveUilmFile(string fileId, string fileName, MemoryStream stream)
        {
            var metaData = new Dictionary<string, object>
            {
                ["FileName"] = new { Type = "String", Value = fileName },
                ["Report"] = new { Type = "String", Value = "UILM Export Data" }
            };

            var result = await _storageHelperService.SaveIntoStorage(stream, fileId, fileName, metaData, "Blocks-Language-Export");
            if (result)
            {
                _logger.LogInformation("SaveUilmFile: Uploaded fileName={FileName}, fileId={NewFileId}", fileName, fileId);
                
                // Create UilmExportedFile entry in DB after successful storage
                await CreateUilmExportedFileEntryAsync(fileId, fileName);
            }
            else
            {
                _logger.LogError("SaveUilmFile: Error in saving file");
            }

            return result;
        }

        private async Task CreateUilmExportedFileEntryAsync(string fileId, string fileName)
        {
            try
            {
                var exportedFile = new UilmExportedFile
                {
                    FileId = fileId,
                    FileName = fileName,
                    CreateDate = DateTime.UtcNow,
                    CreatedBy = BlocksContext.GetContext()?.UserId ?? "System"
                };
                
                await _keyRepository.SaveUilmExportedFileAsync(exportedFile);
                _logger.LogInformation("SaveUilmFile: Created UilmExportedFile entry for fileId={FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError("SaveUilmFile: Failed to create UilmExportedFile entry for fileId={FileId}, Error: {Error}", fileId, ex.Message);
                // Don't fail the entire operation if just the DB entry creation fails
            }
        }

        private async Task<bool> GenerateJsonFile(List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting, List<string> requestedLanguages)
        {
            var jsonOutputGenerator = _serviceProvider.GetService<JsonOutputGeneratorService>();

            // Get all languages from BlocksLanguage collection
            var allLanguages = await _keyRepository.GetAllLanguagesAsync(string.Empty);

            // Filter languages if specific languages are requested
            if (requestedLanguages != null && requestedLanguages.Any())
            {
                allLanguages = allLanguages.Where(l => requestedLanguages.Contains(l.LanguageCode)).ToList();
            }

            var jsonString = await jsonOutputGenerator.GenerateAsync<string>(allLanguages, applications, resourceKeys, languageSetting.LanguageCode);
            if (string.IsNullOrEmpty(jsonString))
            {
                _logger.LogError("GenerateAndWriteFile: Json is null");
                return false;
            }
            var fileBytes = Encoding.UTF8.GetBytes(jsonString);
            var jsonStream = new MemoryStream(fileBytes.ToArray());
            var JsonFileName = "uilm_json_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json";
            return await SaveUilmFile(fileId, JsonFileName, jsonStream);
        }

        private async Task<bool> GenerateCsvFile(List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting, List<string> requestedLanguages)
        {
            var csvOutputGenerator = _serviceProvider.GetService<CsvOutputGeneratorService>();

            // Get all languages from BlocksLanguage collection
            var allLanguages = await _keyRepository.GetAllLanguagesAsync(string.Empty);

            // Filter languages if specific languages are requested
            if (requestedLanguages != null && requestedLanguages.Any())
            {
                allLanguages = allLanguages.Where(l => requestedLanguages.Contains(l.LanguageCode)).ToList();
            }

            var stream = await csvOutputGenerator.GenerateAsync<MemoryStream>(allLanguages, applications, resourceKeys, languageSetting.LanguageCode);
            if (stream is null)
            {
                _logger.LogError("GenerateAndWriteFile: Csv Stream is null");
                return false;
            }
            var csvFileName = "uilm_csv_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            return await SaveUilmFile(fileId, csvFileName, stream);
        }

        private async Task<bool> GenerateXlfFile(List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting, List<string> requestedLanguages,
            string referenceFileId, string projectKey)
        {
            // Filter languages if specific languages are requested
            var languages = requestedLanguages != null && requestedLanguages.Any()
                ? requestedLanguages
                : (await _keyRepository.GetAllLanguagesAsync(string.Empty)).Select(l => l.LanguageCode).ToList();

            // If reference file is provided, use it as template
            if (!string.IsNullOrWhiteSpace(referenceFileId))
            {
                _logger.LogInformation("GenerateXlfFile: Using reference file as template with ID: {ReferenceFileId}", referenceFileId);

                var (fileData, stream) = await GetFileStream(referenceFileId, projectKey);
                if (fileData is null || stream is null)
                {
                    _logger.LogError("GenerateXlfFile: Failed to load reference file with ID: {ReferenceFileId}", referenceFileId);
                    return false;
                }

                // Generate XLF files for each language using the reference as template
                var exportStreamFileMap = await GetLanguageStreamMapFromTemplate(languages, stream, resourceKeys);

                var zipStream = CreateZipStream(exportStreamFileMap);
                var xlfZipFileName = "uilm_xlf_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";

                return await SaveUilmFile(fileId, xlfZipFileName, zipStream);
            }
            else
            {
                // No reference file - generate from scratch
                var xlfOutputGenerator = _serviceProvider.GetService<XlfOutputGeneratorService>();
                var allLanguages = await _keyRepository.GetAllLanguagesAsync(string.Empty);

                if (requestedLanguages != null && requestedLanguages.Any())
                {
                    allLanguages = allLanguages.Where(l => requestedLanguages.Contains(l.LanguageCode)).ToList();
                }

                var stream = await xlfOutputGenerator.GenerateAsync<MemoryStream>(allLanguages, applications, resourceKeys, languageSetting.LanguageCode, null);
                if (stream is null)
                {
                    _logger.LogError("GenerateAndWriteFile: XLF ZIP Stream is null");
                    return false;
                }

                var xlfZipFileName = "uilm_xlf_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";
                return await SaveUilmFile(fileId, xlfZipFileName, stream);
            }
        }

        private async Task<Dictionary<string, MemoryStream>> GetLanguageStreamMapFromTemplate(List<string> languages, Stream referenceStream, List<BlocksLanguageKey> resourceKeys)
        {
            var exportStreamFileMap = new Dictionary<string, MemoryStream>();

            foreach (var language in languages)
            {
                _logger.LogInformation("GetLanguageStreamMapFromTemplate: Processing language: {Language}", language);

                // Build resource key-value map for this language from database
                var resourceKeyValueMap = new Dictionary<string, string>();

                foreach (var dbResource in resourceKeys)
                {
                    var resource = dbResource?.Resources?.FirstOrDefault(x => x.Culture == language);
                    if (resource != null && !string.IsNullOrEmpty(dbResource?.KeyName))
                    {
                        resourceKeyValueMap.TryAdd(dbResource.KeyName, resource.Value);
                    }
                }

                // Copy the reference stream for this language
                using (MemoryStream copyStream = new MemoryStream())
                {
                    referenceStream.Position = 0;
                    referenceStream.CopyTo(copyStream);
                    copyStream.Position = 0;

                    // Write database values into the XLF template
                    var exportStream = WriteToXlf(copyStream, resourceKeyValueMap, language);

                    var xlfFileName = $"messages.{language}.xlf";

                    exportStreamFileMap.Add(xlfFileName, exportStream);
                }
            }

            return exportStreamFileMap;
        }

        private MemoryStream WriteToXlf(Stream templateStream, Dictionary<string, string> resourceKeyValueMap, string targetLanguage)
        {
            try
            {
                XNamespace ns = "urn:oasis:names:tc:xliff:document:1.2";
                var document = XDocument.Load(templateStream);

                var fileElements = document.Root?.Elements(ns + "file");
                if (fileElements == null)
                {
                    _logger.LogWarning("WriteToXlf: No file elements found in XLF template");
                    return new MemoryStream();
                }

                // Update the target language attribute
                foreach (var fileElement in fileElements)
                {
                    fileElement.SetAttributeValue("target-language", targetLanguage);

                    var body = fileElement.Element(ns + "body");
                    if (body == null) continue;

                    var transUnits = body.Elements(ns + "trans-unit");
                    foreach (var transUnit in transUnits)
                    {
                        var keyName = transUnit.Attribute("resname")?.Value;
                        if (string.IsNullOrEmpty(keyName)) continue;

                        // If we have a value from database, update the target element
                        if (resourceKeyValueMap.TryGetValue(keyName, out var value) && !string.IsNullOrEmpty(value))
                        {
                            var targetElement = transUnit.Element(ns + "target");
                            if (targetElement != null)
                            {
                                targetElement.Value = value;
                                targetElement.SetAttributeValue("state", "translated");
                            }
                            else
                            {
                                // Create target element if it doesn't exist
                                transUnit.Add(new XElement(ns + "target",
                                    new XAttribute("state", "translated"),
                                    value));
                            }
                        }
                    }
                }

                // Write the updated XML to a new stream
                var outputStream = new MemoryStream();
                document.Save(outputStream);
                outputStream.Position = 0;

                _logger.LogInformation("WriteToXlf: Successfully updated XLF for language: {Language} with {Count} keys", targetLanguage, resourceKeyValueMap.Count);

                return outputStream;
            }
            catch (Exception ex)
            {
                _logger.LogError("WriteToXlf: Error updating XLF template: {Error}", ex.Message);
                return new MemoryStream();
            }
        }

        private MemoryStream CreateZipStream(Dictionary<string, MemoryStream> fileStreamMap)
        {
            var zipStream = new MemoryStream();

            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var kvp in fileStreamMap)
                {
                    var fileName = kvp.Key;
                    var fileStream = kvp.Value;

                    var zipEntry = archive.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Optimal);

                    using (var entryStream = zipEntry.Open())
                    {
                        fileStream.Position = 0;
                        fileStream.CopyTo(entryStream);
                    }

                    _logger.LogInformation("CreateZipStream: Added {FileName} to ZIP archive", fileName);
                }
            }

            zipStream.Position = 0;
            _logger.LogInformation("CreateZipStream: Created ZIP with {Count} files", fileStreamMap.Count);

            return zipStream;
        }


        private async Task<List<BlocksLanguageKey>> GetLanguageResourceKeys(List<string> appIds = null, DateTime startDate = default, DateTime endDate = default)
        {
            List<BlocksLanguageKey> resourceKeys = null;

            if (appIds != null && appIds.Count > 0)
            {
                resourceKeys = await _keyRepository.GetUilmResourceKeys(x =>
                    appIds.Contains(x.ModuleId),
                    _blocksBaseCommand?.ClientTenantId);
            }
            else
            {
                resourceKeys = await _keyRepository.GetUilmResourceKeys(x => true,
                    _blocksBaseCommand?.ClientTenantId);
            }

            return resourceKeys;
        }

        public async Task PublishUilmExportNotification(bool response, string fileId, string? messageCoRelationId, string tenantId)
        {
            var result = await _notificationService.NotifyExportEvent(response, fileId, messageCoRelationId, tenantId);
            if (result)
            {
                _logger.LogInformation("Notification: sent succussfully messageCoRelationId: {MessageCoRelationId}, fileId={FileId}", messageCoRelationId, fileId);
            }
            else
            {
                _logger.LogError("Notification: sending failed messageCoRelationId: {MessageCoRelationId}, fileId={FileId}", messageCoRelationId, fileId);
            }
        }
        
        public async Task PublishTranslateAllNotification(bool response, string? messageCoRelationId)
        {
            var result = await _notificationService.NotifyTranslateAllEvent(response, messageCoRelationId);
            if (result)
            {
                _logger.LogInformation("Notification: sent succussfully for TranslateAllEvent with messageCoRelationId: {MessageCoRelationId}", messageCoRelationId);
            }
            else
            {
                _logger.LogError("Notification: sending failed for TranslateAllEvent with messageCoRelationId: {MessageCoRelationId}", messageCoRelationId);
            }
        }

        public async Task PublishTranslateBlocksLanguageKeyNotification(bool response, string? messageCoRelationId)
        {
            var result = await _notificationService.NotifyTranslateBlocksLanguageKeyEvent(response, messageCoRelationId);
            if (result)
            {
                _logger.LogInformation("Notification: sent successfully for TranslateBlocksLanguageKeyEvent with messageCoRelationId: {MessageCoRelationId}", messageCoRelationId);
            }
            else
            {
                _logger.LogError("Notification: sending failed for TranslateBlocksLanguageKeyEvent with messageCoRelationId: {MessageCoRelationId}", messageCoRelationId);
            }
        }

        public async Task PublishEnvironmentDataMigrationNotification(bool response, string? messageCoRelationId, string projectKey, string targetedProjectKey)
        {
            var result = await _notificationService.NotifyEnvironmentDataMigrationEvent(response, messageCoRelationId, projectKey, targetedProjectKey);
            if (result)
            {
                _logger.LogInformation("Notification: sent successfully for EnvironmentDataMigrationEvent with messageCoRelationId: {MessageCoRelationId}, ProjectKey: {ProjectKey}, TargetedProjectKey: {TargetedProjectKey}", 
                    messageCoRelationId, projectKey, targetedProjectKey);
            }
            else
            {
                _logger.LogError("Notification: sending failed for EnvironmentDataMigrationEvent with messageCoRelationId: {MessageCoRelationId}, ProjectKey: {ProjectKey}, TargetedProjectKey: {TargetedProjectKey}", 
                    messageCoRelationId, projectKey, targetedProjectKey);
            }
        }

        public async Task<BaseMutationResponse> DeleteCollectionsAsync(DeleteCollectionsRequest request)
        {
            _logger.LogInformation("Delete collections operation started");

            if (request.Collections == null || !request.Collections.Any())
            {
                _logger.LogWarning("Delete collections operation ended - No collections specified");
                return new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "Collections", "At least one collection must be specified" }
                    }
                };
            }

            var validCollections = new List<string> { "BlocksLanguageKeys", "BlocksLanguages", "BlocksLanguageModules", "UilmFiles" };
            var invalidCollections = request.Collections.Where(c => !validCollections.Contains(c)).ToList();

            if (invalidCollections.Any())
            {
                _logger.LogWarning("Delete collections operation ended - Invalid collections specified: {InvalidCollections}", string.Join(", ", invalidCollections));
                return new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "Collections", $"Invalid collections specified: {string.Join(", ", invalidCollections)}. Valid collections are: {string.Join(", ", validCollections)}" }
                    }
                };
            }

            try
            {
                var deleteResults = await _keyRepository.DeleteCollectionsAsync(request.Collections);
                
                var totalDeleted = deleteResults.Values.Sum();
                _logger.LogInformation("Delete collections operation completed successfully. Collections: {Collections}, Total records deleted: {TotalDeleted}", 
                    string.Join(", ", request.Collections), totalDeleted);

                return new BaseMutationResponse 
                { 
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete collections operation failed");
                return new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "Operation", "Failed to delete collections data. Please try again." }
                    }
                };
            }
        }

        public async Task<BaseMutationResponse> RollbackAsync(RollbackRequest request)
        {
            _logger.LogInformation("Rollback operation started for ItemId: {ItemId}", request.ItemId);

            try
            {
                // Get the timeline entry directly by ItemId
                var timeline = await _keyTimelineRepository.GetTimelineByItemIdAsync(request.ItemId);

                if (timeline == null)
                {
                    _logger.LogWarning("Rollback failed - No timeline found for ItemId: {ItemId}", request.ItemId);
                    return new BaseMutationResponse
                    {
                        IsSuccess = false,
                        Errors = new Dictionary<string, string>
                        {
                            { "ItemId", "No timeline found for the specified key" }
                        }
                    };
                }

                if (timeline.PreviousData == null || string.IsNullOrEmpty(timeline.PreviousData.ItemId))
                {
                    _logger.LogWarning("Rollback failed - No previous data available for ItemId: {ItemId}", request.ItemId);
                    return new BaseMutationResponse
                    {
                        IsSuccess = false,
                        Errors = new Dictionary<string, string>
                        {
                            { "PreviousData", "No previous data available for rollback" }
                        }
                    };
                }

                // Get the current BlocksLanguageKey by PreviousData.ItemId
                var currentKey = await _keyRepository.GetUilmResourceKey(x => x.ItemId == timeline.PreviousData.ItemId, "");
                if (currentKey == null)
                {
                    _logger.LogWarning("Rollback failed - Key not found with ItemId: {ItemId}", timeline.PreviousData.ItemId);
                    return new BaseMutationResponse
                    {
                        IsSuccess = false,
                        Errors = new Dictionary<string, string>
                        {
                            { "Key", "Key not found in database" }
                        }
                    };
                }

                // Store current state for timeline
                var rollbackFromKey = GetBlocksLanguageKey(currentKey);

                // Update the key with previous data
                currentKey.KeyName = timeline.PreviousData.KeyName;
                // currentKey.ModuleId = timeline.PreviousData.ModuleId;
                currentKey.Resources = timeline.PreviousData.Resources;
                currentKey.Routes = timeline.PreviousData.Routes;
                currentKey.IsPartiallyTranslated = timeline.PreviousData.IsPartiallyTranslated;
                currentKey.LastUpdateDate = DateTime.UtcNow;

                // Save the rolled back key
                await _keyRepository.SaveKeyAsync(currentKey);

                // Create timeline entry for the rollback operation
                await CreateKeyTimelineEntryAsync(rollbackFromKey, currentKey, "Rollback");

                _logger.LogInformation("Rollback operation completed successfully for ItemId: {ItemId}", request.ItemId);

                return new BaseMutationResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback operation failed for ItemId: {ItemId}", request.ItemId);
                return new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "Operation", "Rollback operation failed. Please try again." }
                    }
                };
            }
        }

        private async Task CreateKeyTimelineEntryAsync(BlocksLanguageKey? previousKey, BlocksLanguageKey currentKey, string logFrom)
        {
            try
            {
                var context = BlocksContext.GetContext();
                var timeline = new KeyTimeline
                {
                    EntityId = currentKey.ItemId,
                    CurrentData = currentKey,
                    PreviousData = previousKey,
                    LogFrom = logFrom,
                    UserId = context?.UserId ?? "System",
                    CreateDate = DateTime.UtcNow,
                    LastUpdateDate = DateTime.UtcNow
                };

                await _keyTimelineRepository.SaveKeyTimelineAsync(timeline);
                _logger.LogInformation("Timeline entry created for Key {KeyId} from {LogFrom}", currentKey.ItemId, logFrom);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create timeline entry for Key {KeyId}: {Error}", currentKey.ItemId, ex.Message);
                // Don't throw - timeline creation should not break the main operation
            }
        }

        public async Task CreateBulkKeyTimelineEntriesAsync(List<BlocksLanguageKey> keys, string logFrom, string targetedProjectKey)
        {
            try
            {
                if (!keys.Any()) return;

                var context = BlocksContext.GetContext();
                var timelines = keys.Select(key => new KeyTimeline
                {
                    EntityId = key.ItemId,
                    CurrentData = key,
                    PreviousData = null, // For migration, we don't have previous data
                    LogFrom = logFrom,
                    UserId = context?.UserId ?? "System",
                    CreateDate = DateTime.UtcNow,
                    LastUpdateDate = DateTime.UtcNow
                }).ToList();

                await _keyTimelineRepository.BulkSaveKeyTimelinesAsync(timelines, targetedProjectKey);
                _logger.LogInformation("Bulk timeline entries created for {Count} keys from {LogFrom}", keys.Count, logFrom);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create bulk timeline entries for {Count} keys: {Error}", keys.Count, ex.Message);
                // Don't throw - timeline creation should not break the main operation
            }
        }

        public async Task CreateBulkKeyTimelineEntriesAsync(List<BlocksLanguageKey> keys, List<BlocksLanguageKey> previousKeys, string logFrom, string targetedProjectKey)
        {
            try
            {
                if (!keys.Any()) return;

                // Create a dictionary for quick lookup of previous keys by ItemId
                var previousKeyDict = previousKeys?.ToDictionary(k => k.ItemId, k => k) ?? new Dictionary<string, BlocksLanguageKey>();

                var context = BlocksContext.GetContext();
                var timelines = keys.Select(key => new KeyTimeline
                {
                    EntityId = key.ItemId,
                    CurrentData = key,
                    PreviousData = previousKeyDict.TryGetValue(key.ItemId, out var previousKey) ? previousKey : null,
                    LogFrom = logFrom,
                    UserId = context?.UserId ?? "System",
                    CreateDate = DateTime.UtcNow,
                    LastUpdateDate = DateTime.UtcNow
                }).ToList();

                await _keyTimelineRepository.BulkSaveKeyTimelinesAsync(timelines, targetedProjectKey);
                _logger.LogInformation("Bulk timeline entries created for {Count} keys from {LogFrom}", keys.Count, logFrom);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create bulk timeline entries for {Count} keys: {Error}", keys.Count, ex.Message);
                // Don't throw - timeline creation should not break the main operation
            }
        }

        private Key MapBlocksLanguageKeyToKey(BlocksLanguageKey blocksKey, string? projectKey = null)
        {
            return new Key
            {
                ItemId = blocksKey.ItemId,
                KeyName = blocksKey.KeyName,
                ModuleId = blocksKey.ModuleId,
                Resources = blocksKey.Resources,
                Routes = blocksKey.Routes,
                IsPartiallyTranslated = blocksKey.IsPartiallyTranslated,
                IsNewKey = false, // Will be set appropriately in calling context
                LastUpdateDate = blocksKey.LastUpdateDate,
                CreateDate = blocksKey.CreateDate,
                ProjectKey = projectKey
            };
        }

        private BlocksLanguageKey MapKeyToBlocksLanguageKey(Key key)
        {
            return new BlocksLanguageKey
            {
                ItemId = key.ItemId ?? Guid.NewGuid().ToString(),
                KeyName = key.KeyName,
                ModuleId = key.ModuleId,
                Resources = key.Resources,
                Routes = key.Routes ?? new List<string>(),
                IsPartiallyTranslated = key.IsPartiallyTranslated,
                LastUpdateDate = key.LastUpdateDate,
                CreateDate = key.CreateDate,
                TenantId = _tenantId
            };
        }
    }
}
