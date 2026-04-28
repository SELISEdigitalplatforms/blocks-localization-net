using Blocks.Genesis;
using ClosedXML.Excel;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Services.HelperService;
using DomainService.Shared.Entities;
using DomainService.Shared.Events;
using DomainService.Storage;
using DomainService.Shared.Utilities;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StorageDriver;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Xunit;
using BlocksLanguageKey = DomainService.Repositories.BlocksLanguageKey;
using BlocksLanguageModule = DomainService.Repositories.BlocksLanguageModule;
using KeyModel = DomainService.Services.Key;

namespace XUnitTest
{
    /// <summary>
    /// Targets the explicitly-requested uncovered methods on KeyManagementService:
    /// ImportUilmFile, ImportJsonFile, GetFileStream, ImportExcelFile,
    /// ProcessExcelCells, SaveUilmFile, GenerateXlfFile, GetLanguageStreamMapFromTemplate.
    /// Private methods are exercised via reflection so HTTP/storage side-effects are bypassed.
    /// </summary>
    public class KeyManagementServiceImportExportTests
    {
        private readonly Mock<IKeyRepository> _keyRepositoryMock = new();
        private readonly Mock<IKeyTimelineRepository> _keyTimelineRepositoryMock = new();
        private readonly Mock<ILanguageFileGenerationHistoryRepository> _historyRepoMock = new();
        private readonly Mock<IValidator<KeyModel>> _validatorMock = new();
        private readonly Mock<ILogger<KeyManagementService>> _loggerMock = new();
        private readonly Mock<ILanguageManagementService> _languageServiceMock = new();
        private readonly Mock<IModuleManagementService> _moduleServiceMock = new();
        private readonly Mock<IMessageClient> _messageClientMock = new();
        private readonly Mock<IAssistantService> _assistantServiceMock = new();
        private readonly Mock<IStorageDriverService> _storageDriverServiceMock = new();
        private readonly Mock<INotificationService> _notificationServiceMock = new();
        private readonly Mock<IGlossaryRepository> _glossaryRepositoryMock = new();
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly StorageHelper _storageHelper;
        private readonly KeyManagementService _service;

        public KeyManagementServiceImportExportTests()
        {
            _storageHelper = new StorageHelper(new Mock<ILogger<StorageHelper>>().Object, _storageDriverServiceMock.Object);

            // Default-safe repository mocks so private pipelines (ProcessJsonFile / ProcessExcelCells) don't NRE.
            _keyRepositoryMock.Setup(r => r.GetUilmApplications<BlocksLanguageModule>(It.IsAny<Expression<Func<BlocksLanguageModule, bool>>>()))
                .ReturnsAsync(new List<BlocksLanguageModule>());
            _keyRepositoryMock.Setup(r => r.GetUilmResourceKey(It.IsAny<Expression<Func<BlocksLanguageKey, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync((BlocksLanguageKey?)null);
            _keyRepositoryMock.Setup(r => r.UpdateUilmResourceKeysForChangeAll(It.IsAny<List<BlocksLanguageKey>>()))
                .ReturnsAsync(0L);
            _keyRepositoryMock.Setup(r => r.InsertUilmResourceKeys(It.IsAny<List<BlocksLanguageKey>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _keyRepositoryMock.Setup(r => r.UpdateBulkUilmApplications(It.IsAny<List<BlocksLanguageModule>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _keyRepositoryMock.Setup(r => r.InsertUilmApplications(It.IsAny<List<BlocksLanguageModule>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _keyRepositoryMock.Setup(r => r.UpdateKeysCountOfAppAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _moduleServiceMock.Setup(m => m.GetModulesAsync(It.IsAny<string>())).ReturnsAsync(new List<BlocksLanguageModule>());

            _service = new KeyManagementService(
                _keyRepositoryMock.Object,
                _keyTimelineRepositoryMock.Object,
                _historyRepoMock.Object,
                _validatorMock.Object,
                _loggerMock.Object,
                _languageServiceMock.Object,
                _moduleServiceMock.Object,
                _messageClientMock.Object,
                _assistantServiceMock.Object,
                _storageDriverServiceMock.Object,
                _storageHelper,
                _serviceProviderMock.Object,
                _notificationServiceMock.Object,
                _glossaryRepositoryMock.Object);
        }

        private static MethodInfo PrivateMethod(string name) =>
            typeof(KeyManagementService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Private method '{name}' not found");

        private async Task<T> InvokeAsync<T>(string methodName, params object?[] args)
        {
            var task = (Task<T>)PrivateMethod(methodName).Invoke(_service, args)!;
            return await task;
        }

        private Task InvokeAsync(string methodName, params object?[] args)
        {
            var task = (Task)PrivateMethod(methodName).Invoke(_service, args)!;
            return task;
        }

        private static FileResponse MakeFileResponse(string name, string url = "http://example.invalid/file")
            => new FileResponse { ItemId = "f1", Name = name, Url = url };

        // =============== ImportJsonFile ===============

        [Fact]
        public async Task ImportJsonFile_ValidJson_ReturnsTrueAndTraversesPipeline()
        {
            var json = "[{\"_id\":\"1\",\"ModuleId\":\"m1\",\"Module\":\"auth\",\"KeyName\":\"hello\",\"IsPartiallyTranslated\":false," +
                       "\"Resources\":[{\"Culture\":\"en-US\",\"Value\":\"Hello\"}]}]";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var file = MakeFileResponse("sample.json");

            var result = await InvokeAsync<bool>("ImportJsonFile", stream, file);

            result.Should().BeTrue();
            // Went through ProcessJsonFile → SaveUilmResourceKey → InsertUilmResourceKeys (new key branch).
            _keyRepositoryMock.Verify(
                r => r.InsertUilmResourceKeys(It.Is<List<BlocksLanguageKey>>(l => l.Any(k => k.KeyName == "hello")), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ImportJsonFile_InvalidJson_ReturnsFalseViaCatchBranch()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ not-valid-json"));
            var file = MakeFileResponse("bad.json");

            var result = await InvokeAsync<bool>("ImportJsonFile", stream, file);

            result.Should().BeFalse();
        }

        // =============== ImportExcelFile / ProcessExcelCells ===============

        private static MemoryStream BuildXlsxStream(Action<IXLWorksheet> populate, string sheetName = "Resources")
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(sheetName);
            populate(ws);
            var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public async Task ImportExcelFile_HappyPath_ReturnsTrueAndInsertsViaProcessExcelCells()
        {
            var stream = BuildXlsxStream(ws =>
            {
                ws.Cell(1, 1).Value = "ItemId";
                ws.Cell(1, 2).Value = "ModuleId";
                ws.Cell(1, 3).Value = "Module";
                ws.Cell(1, 4).Value = "KeyName";
                ws.Cell(1, 5).Value = "en-US";
                ws.Cell(1, 6).Value = "en-US_CharacterLength";

                ws.Cell(2, 1).Value = "k1";
                ws.Cell(2, 2).Value = "m1";
                ws.Cell(2, 3).Value = "auth";
                ws.Cell(2, 4).Value = "welcome.message";
                ws.Cell(2, 5).Value = "Welcome";
                ws.Cell(2, 6).Value = 7;
            });
            var file = MakeFileResponse("data.xlsx");

            var result = await InvokeAsync<bool>("ImportExcelFile", (Stream)stream, file);

            result.Should().BeTrue();
            _keyRepositoryMock.Verify(
                r => r.InsertUilmResourceKeys(It.Is<List<BlocksLanguageKey>>(l => l.Any(k => k.KeyName == "welcome.message")), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ImportExcelFile_MissingRequiredColumns_ReturnsFalse()
        {
            var stream = BuildXlsxStream(ws =>
            {
                // Present a column that is NOT in the required system-columns so the loop populates
                // but the required-columns validation fails.
                ws.Cell(1, 1).Value = "SomeOtherColumn";
                ws.Cell(2, 1).Value = "value";
            });
            var file = MakeFileResponse("missing-cols.xlsx");

            var result = await InvokeAsync<bool>("ImportExcelFile", (Stream)stream, file);

            result.Should().BeFalse();
            _keyRepositoryMock.Verify(
                r => r.InsertUilmResourceKeys(It.IsAny<List<BlocksLanguageKey>>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ImportExcelFile_MalformedStream_ReturnsFalseViaCatchBranch()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-real-xlsx-file"));
            var file = MakeFileResponse("broken.xlsx");

            var result = await InvokeAsync<bool>("ImportExcelFile", (Stream)stream, file);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessExcelCells_InvokedDirectly_PopulatesInsertList()
        {
            // Prepare a worksheet and invoke ProcessExcelCells directly via reflection to
            // assert coverage of its loop body independent of ImportExcelFile's wrapper.
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Resources");
            ws.Cell(1, 1).Value = "ItemId";
            ws.Cell(1, 2).Value = "ModuleId";
            ws.Cell(1, 3).Value = "Module";
            ws.Cell(1, 4).Value = "KeyName";
            ws.Cell(1, 5).Value = "en-US";
            ws.Cell(2, 1).Value = "k1";
            ws.Cell(2, 2).Value = "m1";
            ws.Cell(2, 3).Value = "auth";
            ws.Cell(2, 4).Value = "greet";
            ws.Cell(2, 5).Value = "Hi";

            var columns = new Dictionary<string, string>
            {
                { "ItemId", "A" }, { "ModuleId", "B" }, { "Module", "C" }, { "KeyName", "D" }, { "en-US", "E" }
            };
            var languages = new Dictionary<string, string> { { "en-US", "E" } };
            var uilmResourceKeys = new List<BlocksLanguageKey>();

            await InvokeAsync("ProcessExcelCells", ws, columns, languages, uilmResourceKeys);

            _keyRepositoryMock.Verify(
                r => r.InsertUilmResourceKeys(
                    It.Is<List<BlocksLanguageKey>>(l => l.Any(k => k.KeyName == "greet" && k.Resources.Any(r => r.Value == "Hi"))),
                    It.IsAny<string>()),
                Times.Once);
        }

        // =============== GetLanguageStreamMapFromTemplate ===============

        private const string ReferenceXlfTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en-US"" target-language=""en-US"" datatype=""plaintext"" original=""auth"">
    <body>
      <trans-unit id=""tu1""><source>welcome.message</source><target></target></trans-unit>
      <trans-unit id=""tu2""><source>goodbye.message</source></trans-unit>
    </body>
  </file>
</xliff>";

        [Fact]
        public async Task GetLanguageStreamMapFromTemplate_ProducesStreamPerLanguage_WithMatchedKeysWritten()
        {
            using var refStream = new MemoryStream(Encoding.UTF8.GetBytes(ReferenceXlfTemplate));
            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "k1", KeyName = "welcome.message", ModuleId = "m1",
                    Resources = new[]
                    {
                        new DomainService.Services.Resource { Culture = "de-DE", Value = "Willkommen" }
                    }
                },
                // Key without matching resource for "de-DE" is skipped silently
                new BlocksLanguageKey
                {
                    ItemId = "k2", KeyName = "goodbye.message", ModuleId = "m1",
                    Resources = new[]
                    {
                        new DomainService.Services.Resource { Culture = "en-US", Value = "Bye" }
                    }
                },
                // Null resources – exercises the `dbResource?.Resources?.FirstOrDefault` null branch
                new BlocksLanguageKey { ItemId = "k3", KeyName = "null.key", ModuleId = "m1" }
            };

            var method = typeof(KeyManagementService).GetMethod("GetLanguageStreamMapFromTemplate", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var task = (Task<Dictionary<string, MemoryStream>>)method.Invoke(_service,
                new object[] { new List<string> { "de-DE", "fr-FR" }, refStream, keys })!;
            var map = await task;

            map.Should().HaveCount(2);
            map.Should().ContainKey("messages.de.xlf");
            map.Should().ContainKey("messages.fr.xlf");

            // The de-DE file should contain the translated value from the DB key/value map
            var deStreamContent = Encoding.UTF8.GetString(map["messages.de.xlf"].ToArray());
            deStreamContent.Should().Contain("Willkommen");
            deStreamContent.Should().Contain("target-language=\"de-DE\"");
        }

        [Fact]
        public async Task GetLanguageStreamMapFromTemplate_LanguageWithoutHyphen_UsesFullCodeAsFileName()
        {
            using var refStream = new MemoryStream(Encoding.UTF8.GetBytes(ReferenceXlfTemplate));
            var method = typeof(KeyManagementService).GetMethod("GetLanguageStreamMapFromTemplate", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var task = (Task<Dictionary<string, MemoryStream>>)method.Invoke(_service,
                new object[] { new List<string> { "ja" }, refStream, new List<BlocksLanguageKey>() })!;
            var map = await task;

            map.Should().ContainKey("messages.ja.xlf");
        }

        // =============== GenerateXlfFile (no-reference branch only) ===============

        [Fact]
        public async Task GenerateXlfFile_NoReferenceFile_NullGeneratorStream_ReturnsFalse()
        {
            // Route through the else-branch (no referenceFileId). Register a real XlfOutputGeneratorService
            // so `_serviceProvider.GetService<XlfOutputGeneratorService>()` returns non-null.
            _serviceProviderMock
                .Setup(x => x.GetService(typeof(XlfOutputGeneratorService)))
                .Returns(new XlfOutputGeneratorService(new Mock<ILogger<XlfOutputGeneratorService>>().Object));

            _keyRepositoryMock.Setup(r => r.GetAllLanguagesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<BlocksLanguage>
                {
                    new BlocksLanguage { LanguageCode = "en-US" }
                });

            var method = typeof(KeyManagementService).GetMethod("GenerateXlfFile", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var task = (Task<bool>)method.Invoke(_service, new object?[]
            {
                new List<BlocksLanguageModule>(),
                new List<BlocksLanguageKey>(),
                "file-1",
                new BlocksLanguage { LanguageCode = "en-US" },
                (List<string>?)null,
                (string?)null,      // no reference file → else branch
                "proj"
            })!;

            var result = await task;

            // No target languages available for XlfOutputGenerator (only source=en-US), generator returns null → false.
            result.Should().BeFalse();
        }

        // =============== SaveUilmFile — unreachable without HTTP seam ===============
        // SaveUilmFile calls StorageHelper.SaveIntoStorage which performs a real HTTP PUT
        // via HttpClient.SendAsync. The HttpClient is instantiated inside StorageHelper
        // with no seam for mocking, so a pure unit test would perform a real network call
        // and fail non-deterministically. See test-suite summary for refactor recommendation.

        // =============== ImportUilmFile / GetFileStream(string,string) ===============

        [Fact]
        public async Task ImportUilmFile_FileDataNull_ReturnsFalseViaGetFileStreamGuard()
        {
            _storageDriverServiceMock
                .Setup(s => s.GetUrlForDownloadFileAsync(It.IsAny<GetFileRequest>()))
                .ReturnsAsync((FileResponse?)null!);

            var result = await _service.ImportUilmFile(new UilmImportEvent
            {
                FileId = "missing", ProjectKey = "proj"
            });

            result.Should().BeFalse();
            _loggerMock.Verify(
                l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }
    }
}
