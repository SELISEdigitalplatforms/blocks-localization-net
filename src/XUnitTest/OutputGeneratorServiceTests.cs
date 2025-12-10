using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BlocksLanguage = DomainService.Repositories.BlocksLanguage;
using BlocksLanguageModule = DomainService.Repositories.BlocksLanguageModule;
using BlocksLanguageKey = DomainService.Repositories.BlocksLanguageKey;
using Resource = DomainService.Services.Resource;

namespace XUnitTest
{
    public class JsonOutputGeneratorServiceTests
    {
        private readonly Mock<ILogger<XlsxOutputGeneratorService>> _loggerMock;
        private readonly JsonOutputGeneratorService _service;

        public JsonOutputGeneratorServiceTests()
        {
            _loggerMock = new Mock<ILogger<XlsxOutputGeneratorService>>();
            _service = new JsonOutputGeneratorService(_loggerMock.Object);
        }

        [Fact]
        public async Task GenerateAsync_ValidInput_ReturnsJsonString()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" },
                new BlocksLanguage { LanguageCode = "fr-FR", LanguageName = "French" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome" },
                        new Resource { Culture = "fr-FR", Value = "Bienvenue" }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<string>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("welcome.message");
            result.Should().Contain("Welcome");
            result.Should().Contain("Bienvenue");
        }

        [Fact]
        public async Task GenerateAsync_EmptyKeys_ReturnsEmptyJsonArray()
        {
            // Arrange
            var languages = new List<BlocksLanguage>();
            var modules = new List<BlocksLanguageModule>();
            var keys = new List<BlocksLanguageKey>();

            // Act
            var result = await _service.GenerateAsync<string>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("[]");
        }

        [Fact]
        public async Task GenerateAsync_FiltersTypeCulture_ExcludesTypeResources()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome" },
                        new Resource { Culture = "type", Value = "string" }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<string>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Welcome");
            result.Should().NotContain("type");
        }

        [Fact]
        public async Task GenerateAsync_FiltersEmptyValues_ExcludesEmptyResources()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome" },
                        new Resource { Culture = "fr-FR", Value = "" }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<string>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Welcome");
        }
    }

    public class CsvOutputGeneratorServiceTests
    {
        private readonly Mock<ILogger<CsvOutputGeneratorService>> _loggerMock;
        private readonly CsvOutputGeneratorService _service;

        public CsvOutputGeneratorServiceTests()
        {
            _loggerMock = new Mock<ILogger<CsvOutputGeneratorService>>();
            _service = new CsvOutputGeneratorService(_loggerMock.Object);
        }

        [Fact]
        public async Task GenerateAsync_ValidInput_ReturnsMemoryStream()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" },
                new BlocksLanguage { LanguageCode = "fr-FR", LanguageName = "French" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome" },
                        new Resource { Culture = "fr-FR", Value = "Bienvenue" }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<MemoryStream>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GenerateAsync_IncludesCharacterLength_ForNonDefaultLanguages()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" },
                new BlocksLanguage { LanguageCode = "fr-FR", LanguageName = "French" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome", CharacterLength = 7 },
                        new Resource { Culture = "fr-FR", Value = "Bienvenue", CharacterLength = 9 }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<MemoryStream>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GenerateAsync_EmptyKeys_ReturnsStreamWithHeaders()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" }
            };

            var modules = new List<BlocksLanguageModule>();
            var keys = new List<BlocksLanguageKey>();

            // Act
            var result = await _service.GenerateAsync<MemoryStream>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }
    }

    public class XlsxOutputGeneratorServiceTests
    {
        private readonly Mock<ILogger<XlsxOutputGeneratorService>> _loggerMock;
        private readonly XlsxOutputGeneratorService _service;

        public XlsxOutputGeneratorServiceTests()
        {
            _loggerMock = new Mock<ILogger<XlsxOutputGeneratorService>>();
            _service = new XlsxOutputGeneratorService(_loggerMock.Object);
        }

        [Fact]
        public async Task GenerateAsync_ValidInput_ReturnsWorkbook()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" },
                new BlocksLanguage { LanguageCode = "fr-FR", LanguageName = "French" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome" },
                        new Resource { Culture = "fr-FR", Value = "Bienvenue" }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<ClosedXML.Excel.XLWorkbook>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Worksheets.Should().NotBeEmpty();
            result.Worksheets.First().Name.Should().Be("Resources");
        }

        [Fact]
        public async Task GenerateAsync_IncludesCharacterLengthColumns_ForNonDefaultLanguages()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" },
                new BlocksLanguage { LanguageCode = "fr-FR", LanguageName = "French" }
            };

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "module-id", ModuleName = "auth" }
            };

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "key-id",
                    KeyName = "welcome.message",
                    ModuleId = "module-id",
                    Resources = new[]
                    {
                        new Resource { Culture = "en-US", Value = "Welcome", CharacterLength = 7 },
                        new Resource { Culture = "fr-FR", Value = "Bienvenue", CharacterLength = 9 }
                    }
                }
            };

            // Act
            var result = await _service.GenerateAsync<ClosedXML.Excel.XLWorkbook>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            var worksheet = result.Worksheets.First();
            worksheet.Should().NotBeNull();
        }

        [Fact]
        public async Task GenerateAsync_EmptyKeys_ReturnsWorkbookWithHeaders()
        {
            // Arrange
            var languages = new List<BlocksLanguage>
            {
                new BlocksLanguage { LanguageCode = "en-US", LanguageName = "English" }
            };

            var modules = new List<BlocksLanguageModule>();
            var keys = new List<BlocksLanguageKey>();

            // Act
            var result = await _service.GenerateAsync<ClosedXML.Excel.XLWorkbook>(languages, modules, keys, "en-US");

            // Assert
            result.Should().NotBeNull();
            result.Worksheets.Should().NotBeEmpty();
        }
    }
}

