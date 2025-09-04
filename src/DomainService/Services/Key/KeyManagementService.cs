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
                Key? previousKey = null;
                bool isNewKey = existingRepoKey == null;
                
                if (!isNewKey && existingRepoKey != null)
                {
                    previousKey = MapBlocksLanguageKeyToKey(existingRepoKey, key.ProjectKey);
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

                // Create current key state for timeline
                var currentKey = new Key
                {
                    ItemId = repoKey.ItemId,
                    KeyName = key.KeyName,
                    ModuleId = key.ModuleId,
                    Resources = key.Resources,
                    Routes = key.Routes,
                    IsPartiallyTranslated = key.IsPartiallyTranslated,
                    IsNewKey = isNewKey,
                    LastUpdateDate = repoKey.LastUpdateDate,
                    CreateDate = repoKey.CreateDate,
                    ProjectKey = key.ProjectKey
                };

                // Create timeline entry
                await CreateKeyTimelineEntryAsync(previousKey, currentKey, "KeyController.Save");
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
            repoKey.ModuleId = key.ModuleId;
            repoKey.Resources = key.Resources;
            repoKey.IsPartiallyTranslated = key.IsPartiallyTranslated;
            repoKey.Routes = key.Routes;

            return repoKey;
        }

        public async Task<GetKeysQueryResponse> GetKeysAsync(GetKeysRequest query)
        {
            return await _keyRepository.GetAllKeysAsync(query);
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
                // For deletion, we set current to null and previous to the existing key
                // But since CreateKeyTimelineEntryAsync expects currentKey, we'll create a special delete entry
                var deletedKey = new Key
                {
                    ItemId = key.ItemId,
                    KeyName = key.KeyName,
                    ModuleId = key.ModuleId,
                    Resources = key.Resources,
                    Routes = key.Routes,
                    IsPartiallyTranslated = key.IsPartiallyTranslated,
                    IsNewKey = false,
                    LastUpdateDate = DateTime.UtcNow,
                    CreateDate = key.CreateDate,
                    ProjectKey = key.ProjectKey
                };
                await CreateKeyTimelineEntryAsync(key, deletedKey, "KeyController.Delete");
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


                var resourceKeys = await ProcessChangeAll(request, dbResourceKeys, languageSetting);

                if (resourceKeys.Any())
                {
                    await UpdateResourceKey(resourceKeys, request);
                }

                page++;
            }

            return true;
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
            return resources.Where(x => x.Culture != defaultLanguage && (x.Value == keyName || x.Value == null || x.Value == ""
                                                                || x.Value == $"{defaultResource?.Value} {x.Culture.ToUpper()}"
                                                                || x.Value == $"{defaultResource?.Value}")).ToList();
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
                //ElementDetailContext = resourceKey.Context,
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

        public async Task UpdateResourceKey(List<BlocksLanguageKey> resourceKeys, TranslateAllEvent request)
        {
            var updateCount = await _keyRepository.UpdateUilmResourceKeysForChangeAll(resourceKeys, null, false, null);

            // Create timeline entries for updated keys
            foreach (var resourceKey in resourceKeys)
            {
                try
                {
                    var currentKey = MapBlocksLanguageKeyToKey(resourceKey, request.ProjectKey);
                    // For TranslateAll, we don't have the previous state easily accessible
                    // but we can indicate this was a translation operation
                    await CreateKeyTimelineEntryAsync(null, currentKey, "TranslateAll");
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
            var uilmFile = await _keyRepository.GetUilmFile(request);
            return uilmFile?.Content;
        }

        public async Task SendTranslateAllEvent(TranslateAllRequest request)
        {
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<TranslateAllEvent>
                {
                    ConsumerName = Utilities.Constants.UilmQueue,
                    Payload = new TranslateAllEvent
                    {
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey,
                        DefaultLanguage = request.DefaultLanguage
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
            await _messageClient.SendToConsumerAsync(
                new ConsumerMessage<UilmExportEvent>
                {
                    ConsumerName = Utilities.Constants.UilmImportExportQueue,
                    Payload = new UilmExportEvent
                    {
                        FileId = request.ReferenceFileId,
                        MessageCoRelationId = request.MessageCoRelationId,
                        ProjectKey = request.ProjectKey,
                        AppIds = request.AppIds,
                        CallerTenantId = request.CallerTenantId,
                        EndDate = request.EndDate,
                        StartDate = request.StartDate,
                        Languages = request.Languages,
                        OutputType = request.OutputType
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
            //else if (fileData.Name.EndsWith(".xlf"))
            //{
            //    _format = "XLF";
            //    return await ImportXlfFile(stream, fileData);
            //}

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

                    var cultures = new Dictionary<string, string>();

                    for (int i = 6; i < fields.Length; i++)
                    {
                        if (fields[i].Contains("_CharacterLength"))
                        {
                            continue;
                        }

                        cultures.Add(fields[i].Trim(), fields[i + 1].Trim());
                    }

                    var languageJsonModels = new List<LanguageJsonModel>();

                    while (csv.Read())
                    {
                        var languageJsonModel = new LanguageJsonModel
                        {
                            _id = csv.GetField<string>("_id"),
                            Value = csv.GetField<string>("Value"),
                            KeyName = csv.GetField<string>("KeyName"),
                            Resources = csv.GetField<Resource[]>("Resources"),
                            ModuleId = csv.GetField<string>("ModuleId"),
                            IsPartiallyTranslated = csv.GetField<bool>("IsPartiallyTranslated"),
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

            // var uilmAppTimeLines = new List<BlocksLanguageManagerTimeline>();
            // var uilmResourceKeyTimeLines = new List<BlocksLanguageManagerTimeline>();

            foreach (var languageJsonModel in languageJsonModels)
            {
                var id = languageJsonModel._id;
                var appId = languageJsonModel.ModuleId;
                var isPartiallyTranslated = languageJsonModel.IsPartiallyTranslated;
                var moduleName = dbApplications.First(x => x.ItemId == languageJsonModel.ModuleId)?.ModuleName;
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
                    Value = languageJsonModel.Value,
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
                    uilmResourceKeys.Add(uilmResourceKey);
                }
                //FormatUilmResouceKeyTimeline(uilmResourceKeyTimeLine, olduilmResourceKey, uilmResourceKey);
                //uilmResourceKeyTimeLines.Add(uilmResourceKeyTimeLine);
            }

            await SaveUilmResourceKey(uilmResourceKeys, resourceKeysWithoutId);

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
                applications = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => appIds.Contains(x.ItemId), _blocksBaseCommand?.ClientTenantId);
            }
            else
            {
                applications = await _keyRepository.GetUilmApplications<BlocksLanguageModule>(x => true, _blocksBaseCommand?.ClientTenantId);
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

        private async Task ProcessExcelCells(IXLWorksheet worksheet, Dictionary<string, string> columns, Dictionary<string, string> languages,
            List<BlocksLanguageKey> uilmResourceKeys)
        {
            //List<Language> languageSetting = await _languageManagementService.GetLanguagesAsync();

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
                string resources = worksheet.Cell(i, columns["Resources"]).Value.ToString();
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
                    uilmResourceKeys.Add(uilmResourceKey);
                }

                //FormatUilmResouceKeyTimeline(uilmResourceKeyTimeLine, olduilmResourceKey, uilmResourceKey);
                //uilmResourceKeyTimeLines.Add(uilmResourceKeyTimeLine);
            }

            await SaveUilmResourceKey(uilmResourceKeys, resourceKeysWithoutId);
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

        private async Task SaveUilmResourceKey(List<BlocksLanguageKey> uilmResourceKeys, List<BlocksLanguageKey> resourceKeysWithoutId)
        {
            if (uilmResourceKeys.Any())
            {
                long? updateCount = 0;

                updateCount = await _keyRepository.UpdateUilmResourceKeysForChangeAll(uilmResourceKeys, _blocksBaseCommand?.OrganizationId, _blocksBaseCommand?.IsExternal ?? false, _blocksBaseCommand?.ClientTenantId);

                // Create timeline entries for updated keys
                foreach (var resourceKey in uilmResourceKeys)
                {
                    try
                    {
                        var currentKey = MapBlocksLanguageKeyToKey(resourceKey, null);
                        await CreateKeyTimelineEntryAsync(null, currentKey, "UilmImport.Update");
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
                        var currentKey = MapBlocksLanguageKeyToKey(resourceKey, null);
                        currentKey.IsNewKey = true;
                        await CreateKeyTimelineEntryAsync(null, currentKey, "UilmImport.Insert");
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
                    return await GenerateXlsxFile(languageApplications, languageResourceKeys, request.FileId, languageSettings);
                case OutputType.Json:
                    return await GenerateJsonFile(languageApplications, languageResourceKeys, request.FileId, languageSettings);
                case OutputType.Csv:
                    return await GenerateCsvFile(languageApplications, languageResourceKeys, request.FileId, languageSettings);
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
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting)
        {
            var xlsxOutputGenerator = _serviceProvider.GetService<XlsxOutputGeneratorService>();
            var workBook = await xlsxOutputGenerator.GenerateAsync<XLWorkbook>(languageSetting, applications, resourceKeys, languageSetting.LanguageCode);
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
            }
            else
            {
                _logger.LogError("SaveUilmFile: Error in saving file");
            }

            return result;
        }

        private async Task<bool> GenerateJsonFile(List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting)
        {
            var jsonOutputGenerator = _serviceProvider.GetService<JsonOutputGeneratorService>();
            var jsonString = await jsonOutputGenerator.GenerateAsync<string>(languageSetting, applications, resourceKeys, languageSetting.LanguageCode);
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
            List<BlocksLanguageKey> resourceKeys, string fileId, BlocksLanguage languageSetting)
        {
            var csvOutputGenerator = _serviceProvider.GetService<CsvOutputGeneratorService>();
            var stream = await csvOutputGenerator.GenerateAsync<MemoryStream>(languageSetting, applications, resourceKeys, languageSetting.LanguageCode);
            if (stream is null)
            {
                _logger.LogError("GenerateAndWriteFile: Csv Stream is null");
                return false;
            }
            var csvFileName = "uilm_csv_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            return await SaveUilmFile(fileId, csvFileName, stream);
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

        private async Task CreateKeyTimelineEntryAsync(Key? previousKey, Key currentKey, string logFrom)
        {
            try
            {
                var context = BlocksContext.GetContext();
                var timeline = new KeyTimeline
                {
                    EntityId = currentKey.ItemId,
                    ProjectKey = currentKey.ProjectKey,
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
    }
}
