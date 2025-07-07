using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Shared;
using DomainService.Shared.Events;
using DomainService.Storage;
using FluentValidation;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using StorageDriver;
using ClosedXML.Excel;

namespace DomainService.Services
{
    public class KeyManagementService : IKeyManagementService
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IValidator<Key> _validator;
        private readonly ILogger<KeyManagementService> _logger;
        private readonly ILanguageManagementService _languageManagementService;
        private readonly IModuleManagementService _moduleManagementService;
        private readonly IMessageClient _messageClient;
        private readonly IAssistantService _assistantService;
        private readonly IStorageDriverService _storageDriverService;

        private readonly string _tenantId = BlocksContext.GetContext()?.TenantId ?? "";
        private string _format;

        public KeyManagementService(
            IKeyRepository keyRepository,
            IValidator<Key> validator,
            ILogger<KeyManagementService> logger,
            ILanguageManagementService languageManagementService,
            IModuleManagementService moduleManagementService,
            IMessageClient messageClient,
            IAssistantService assistantService,
            IStorageDriverService storageDriverService)
        {
            _keyRepository = keyRepository;
            _validator = validator;
            _logger = logger;
            _languageManagementService = languageManagementService;
            _moduleManagementService = moduleManagementService;
            _messageClient = messageClient;
            _assistantService = assistantService;
            _storageDriverService = storageDriverService;
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
            var updateCount = await _keyRepository.UpdateUilmResourceKeysForChangeAll(resourceKeys);

            //await _languageManagementRepository.UpdateUilmResourceKeysTimelineForChangeAll(resourceKeyTimelines);

            //await CallWebhook(command);

            _logger.LogInformation($"ChangeAll: Uilm Resource key updated: {updateCount}");
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
            var dbApplications = await GetLanguageApplications(null);

            var uilmApplicationsToBeInserted = new List<UilmApplication>();
            var uilmApplicationsToBeUpdated = new List<UilmApplication>();

            var resourceKeysWithoutId = new List<BlocksLanguageKey>();
            var cultures = languages.Where(x => !x.Key.Contains("_CharacterLength")).ToDictionary(x => x.Key, y => y.Value);

            var excelRows = worksheet.RowsUsed().Count();

            _logger.LogInformation("ImportExcelFile: {Excelrows} UilmResourceKeys Found!", excelRows - 1);

            var uilmAppTimeLines = new List<BlocksLanguageManagerTimeline>();
            var uilmResourceKeyTimeLines = new List<BlocksLanguageManagerTimeline>();

            for (int i = 2; i <= excelRows; i++)
            {
                string id = worksheet.Cell(i, columns["id"]).Value.ToString();
                string appId = worksheet.Cell(i, columns["app id"]).Value.ToString();
                string appName = worksheet.Cell(i, columns["app"]).Value.ToString();
                string keyName = worksheet.Cell(i, columns["key"]).Value.ToString();
                string moduleName = worksheet.Cell(i, columns["module"]).Value.ToString();
                string type = worksheet.Cell(i, columns["type"]).Value.ToString();

                var uilmAppTimeLine = GetBlocksLanguageManagerTimeline();

                appId = HandleUilmApplication(dbApplications, uilmApplicationsToBeInserted, uilmApplicationsToBeUpdated, appId, appName, moduleName,
                            uilmAppTimeLine);

                uilmAppTimeLines.Add(uilmAppTimeLine);

                BlocksLanguageKey uilmResourceKey = new()
                {
                    Id = id,
                    AppId = appId,
                    KeyName = keyName,
                    ModifiedDate = DateTime.UtcNow,
                    Type = type,
                };

                uilmResourceKey.Resources = new Resource[cultures.Count];

                int j = 0;
                foreach (KeyValuePair<string, string> lang in cultures)
                {
                    string resourceValue = worksheet.Cell(i, lang.Value).Value.ToString();
                    int characterLength = 0;

                    var key = lang.Key + "_CharacterLength";
                    if (lang.Key != defaultLanguage && languages.ContainsKey(key))
                    {
                        characterLength = AssignCharacterLengthValue(worksheet, languages, i, key);
                    }

                    uilmResourceKey.Resources[j++] = (new Resource() { Culture = lang.Key, Value = resourceValue, CharacterLength = characterLength });
                }

                var uilmResourceKeyTimeLine = GetBlocksLanguageManagerTimeline();

                var olduilmResourceKey = await GetUilmResourceKey(uilmResourceKey.AppId, uilmResourceKey.KeyName);

                uilmResourceKey.Id = string.IsNullOrWhiteSpace(uilmResourceKey.Id) ? Guid.NewGuid().ToString() : uilmResourceKey.Id;

                if (olduilmResourceKey == null)
                {
                    resourceKeysWithoutId.Add(uilmResourceKey);
                }
                else
                {
                    uilmResourceKey.Id = string.IsNullOrWhiteSpace(olduilmResourceKey.Id) ? uilmResourceKey.Id : olduilmResourceKey.Id;
                    uilmResourceKeys.Add(uilmResourceKey);
                }

                FormatUilmResouceKeyTimeline(uilmResourceKeyTimeLine, olduilmResourceKey, uilmResourceKey);
                uilmResourceKeyTimeLines.Add(uilmResourceKeyTimeLine);
            }

            await SaveUilmResourceKey(uilmResourceKeys, resourceKeysWithoutId);
            await SaveUilmApplication(uilmApplicationsToBeInserted.DistinctBy(x => x.ModuleName).ToList(), uilmApplicationsToBeUpdated.DistinctBy(x => x.ModuleName).ToList());

            uilmResourceKeyTimeLines?.AddRange(uilmAppTimeLines?.DistinctBy(x => x.CurrentData?.UilmApplication?.ModuleName)?.ToList());
            await _uilmRepository.SaveBlocksLanguageManagerTimeLines(uilmResourceKeyTimeLines);
        }

    }
}
