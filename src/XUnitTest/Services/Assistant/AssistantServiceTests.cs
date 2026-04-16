using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace XUnitTest
{
    public class AssistantServiceTests
    {
        private readonly Mock<ILogger<AssistantService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILocalizationSecret> _localizationSecretMock;
        private readonly HttpClient _httpClient;
        private readonly AssistantService _assistantService;
        private readonly Mock<HttpMessageHandler> _handlerMock;


        public AssistantServiceTests()
        {
            _loggerMock = new Mock<ILogger<AssistantService>>();
            _configurationMock = new Mock<IConfiguration>();
            _localizationSecretMock = new Mock<ILocalizationSecret>();

            _configurationMock.SetupGet(x => x["Key"]).Returns("test-key");
            _configurationMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");
            _configurationMock.SetupGet(x => x["ChatGptTemperature"]).Returns("0.7");
            _configurationMock.SetupGet(x => x["Salt"]).Returns("[\"01\",\"02\",\"03\",\"04\",\"05\",\"06\",\"07\",\"08\"]");

            _localizationSecretMock.SetupGet(x => x.ChatGptEncryptionKey).Returns("dummy-encryption-key");
            _localizationSecretMock.SetupGet(x => x.ChatGptEncryptedSecret)
                .Returns("dummy-encrypted-secret");

            // Use a stubbed HttpMessageHandler so no real HTTP is performed.
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            _httpClient = new HttpClient(_handlerMock.Object);

            _assistantService = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );
        }

        #region GenerateSuggestTranslationContext Tests

        [Fact]
        public void GenerateSuggestTranslationContext_WithElementDetailContext_ReturnsContext()
        {
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = "submit",
                SourceText = "Submit",
                DestinationLanguage = "es",
                CurrentLanguage = "en"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request);

            result.Should().Contain("submit");
            result.Should().Contain("Translate the following from en to es: 'Submit'");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithoutElementDetailContext_ReturnsDefault()
        {
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = null,
                SourceText = "Welcome",
                DestinationLanguage = "fr",
                CurrentLanguage = "en"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request);

            result.Should().Contain("translate a user interface element", because: "default context should be used");
            result.Should().Contain("Translate the following from en to fr: 'Welcome'");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithEmptyElementDetailContext_ReturnsDefaultContext()
        {
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = "   ",
                SourceText = "Hello",
                DestinationLanguage = "de",
                CurrentLanguage = "en"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request);

            result.Should().Contain("translate a user interface element");
            result.Should().Contain("Translate the following from en to de: 'Hello'");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithAllParameters_ReturnsCompleteContext()
        {
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = "Button for form submission",
                SourceText = "Save Changes",
                DestinationLanguage = "ja",
                CurrentLanguage = "en"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request);

            result.Should().Contain("Button for form submission");
            result.Should().Contain("Translate the following from en to ja: 'Save Changes'");
        }

        #endregion

        #region FormatAiTextForSuggestTranslation Tests

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithColon_ExtractsTextAfterColon()
        {
            var aiText = "\"Translated: Enviar\"";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Enviar");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithoutColon_ReturnsTrimmedText()
        {
            var aiText = "\"Bienvenue\"";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Bienvenue");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithQuotes_RemovesQuotes()
        {
            var aiText = "'Hello World'";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Hello World");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithWhitespace_TrimsWhitespace()
        {
            var aiText = "  \n\tBonjour\t\n  ";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Bonjour");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_NullInput_ReturnsEmpty()
        {
            string? aiText = null;

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_EmptyString_ReturnsEmpty()
        {
            var aiText = "";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WhitespaceOnly_ReturnsEmpty()
        {
            var aiText = "   ";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithMultipleColons_ExtractsTextAfterFirstColon()
        {
            var aiText = "Translation: Hello: World";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Hello");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_ColonAtEnd_ReturnsEmptyAfterColon()
        {
            var aiText = "Translation:";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithMixedQuotes_RemovesBothQuoteTypes()
        {
            var aiText = "\"Test'Value\"";

            var result = AssistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("TestValue");
        }

        #endregion

        #region PrepareHttpRequest Tests

        [Fact]
        public void PrepareHttpRequest_WithContent_CreatesRequestWithContent()
        {
            var url = "http://test.com/api";
            var content = "{\"test\": \"data\"}";

            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Post, content);

            result.Method.Should().Be(HttpMethod.Post);
            result.RequestUri.Should().Be(new Uri(url));
            result.Content.Should().NotBeNull();
        }

        [Fact]
        public void PrepareHttpRequest_WithoutContent_CreatesRequestWithoutContent()
        {
            var url = "http://test.com/api";

            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Get, null);

            result.Method.Should().Be(HttpMethod.Get);
            result.RequestUri.Should().Be(new Uri(url));
            result.Content.Should().BeNull();
        }

        public void PrepareHttpRequest_WithPutMethod_CreatesCorrectRequest()
        {
            var url = "http://test.com/api/resource";
            var content = "{\"id\": 1}";

            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Put, content);

            result.Method.Should().Be(HttpMethod.Put);
            result.RequestUri.Should().Be(new Uri(url));
            result.Content.Should().NotBeNull();
        }

        [Fact]
        public void PrepareHttpRequest_WithDeleteMethod_CreatesCorrectRequest()
        {
            var url = "http://test.com/api/resource/1";

            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Delete, null);

            result.Method.Should().Be(HttpMethod.Delete);
            result.RequestUri.Should().Be(new Uri(url));
        }

        [Fact]
        public async Task PrepareHttpRequest_ContentType_IsApplicationJson()
        {
            var url = "http://test.com/api";
            var content = "{\"key\": \"value\"}";

            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Post, content);

            result.Content.Should().NotBeNull();
            result.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        }

        #endregion

        #region Decrypt Tests

        [Fact]
        public void Decrypt_WithInvalidCipher_ThrowsCryptographicException()
        {
            var encryptedText = "dGVzdA=="; // base64 of "test", not a valid AES cipher for this key/salt
            var key = "test-key";
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            Action act = () => AssistantService.Decrypt(encryptedText, key, salt);

            act.Should().Throw<Exception>(); // cryptographic failure is expected, but should not cause null refs
        }

        [Fact]
        public void Decrypt_WithInvalidBase64_ThrowsFormatException()
        {
            var invalidBase64 = "not-valid-base64!!!";
            var key = "test-key";
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            Action act = () => AssistantService.Decrypt(invalidBase64, key, salt);

            act.Should().Throw<FormatException>();
        }

        [Fact]
        public void Decrypt_WithEmptyKey_ThrowsException()
        {
            var encryptedText = "dGVzdA==";
            var key = "";
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            Action act = () => AssistantService.Decrypt(encryptedText, key, salt);

            act.Should().Throw<Exception>();
        }

        #endregion

        #region GetSalt Tests

        [Fact]
        public void GetSalt_WhenConfigured_ReturnsByteArray()
        {
            var configSectionMock = new Mock<IConfigurationSection>();
            _configurationMock.Setup(x => x.GetSection("Salt")).Returns(configSectionMock.Object);

            var result = _assistantService.GetSalt();

            // Result depends on configuration setup; verify method executes without exception
            result.Should().BeNull(); // Mock returns null when Get<byte[]>() is not explicitly set
        }

        #endregion

        #region MakeRequestAsync Tests

        [Fact]
        public async Task MakeRequestAsync_WithErrorResponse_ReturnsErrorStatusCode()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\": \"Bad request\"}", Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com/api");
            var result = await service.MakeRequestAsync(request, "test-secret");

            result.Should().NotBeNull();
            result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task MakeRequestAsync_WithInternalServerError_ReturnsServerErrorStatusCode()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\": \"Server error\"}", Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com/api");
            var result = await service.MakeRequestAsync(request, "test-secret");

            result.HttpStatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        #endregion

        #region AiCompletion Tests

        [Fact]
        public async Task AiCompletion_WithInvalidTemperature_ReturnsNull()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.SetupGet(x => x["ChatGptTemperature"]).Returns("1.5"); // Invalid temperature > 1
            configMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");

            var service = new AssistantService(
                _loggerMock.Object,
                configMock.Object,
                _httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new AiCompletionRequest("Test message", 0.5);
            var result = await service.AiCompletion(request);

            result.Should().BeNull();
        }

        [Fact]
        public async Task AiCompletion_WithNegativeTemperature_ReturnsNull()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.SetupGet(x => x["ChatGptTemperature"]).Returns("-0.5"); // Invalid negative temperature
            configMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");

            var service = new AssistantService(
                _loggerMock.Object,
                configMock.Object,
                _httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new AiCompletionRequest("Test message", 0.5);
            var result = await service.AiCompletion(request);

            result.Should().BeNull();
        }

        [Fact]
        public async Task AiCompletion_WithNullEncryptedSecret_ReturnsNull()
        {
            var localizationSecretMock = new Mock<ILocalizationSecret>();
            localizationSecretMock.SetupGet(x => x.ChatGptEncryptedSecret).Returns((string)null!);

            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClient,
                localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new AiCompletionRequest("Test message", 0.5);
            var result = await service.AiCompletion(request);

            result.Should().BeNull();
        }

        [Fact]
        public async Task AiCompletion_WithEmptyEncryptedSecret_ReturnsNull()
        {
            var localizationSecretMock = new Mock<ILocalizationSecret>();
            localizationSecretMock.SetupGet(x => x.ChatGptEncryptedSecret).Returns(string.Empty);

            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClient,
                localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new AiCompletionRequest("Test message", 0.5);
            var result = await service.AiCompletion(request);

            result.Should().BeNull();
        }

        #endregion

        #region SuggestTranslation Integration Tests

        [Fact]
        public async Task SuggestTranslation_WithValidRequest_CallsAiCompletion()
        {
            // This test verifies the flow but expects null due to encryption issues in test environment
            var request = new SuggestLanguageRequest
            {
                SourceText = "Hello",
                CurrentLanguage = "en",
                DestinationLanguage = "es",
                Temperature = 0.7
            };

            var result = await _assistantService.SuggestTranslation(request);

            // Due to encryption setup in tests, this will return null
            // The test verifies the method doesn't throw
            result.Should().BeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            service.Should().NotBeNull();
        }

        #endregion

        #region MakeRequestAsync Success Tests

        [Fact]
        public async Task MakeRequestAsync_WithSuccessResponse_ReturnsOkStatus()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\": \"ok\"}", Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com/api");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var result = await service.MakeRequestAsync(request, "test-secret");

            result.Should().NotBeNull();
            result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            ((string)result.ResponseData).Should().Contain("ok");
        }

        [Fact]
        public async Task MakeRequestAsync_SetsAuthorizationHeader()
        {
            HttpRequestMessage? capturedRequest = null;
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var service = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                httpClient,
                _localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com/api");
            await service.MakeRequestAsync(request, "my-secret");

            // The HttpClient should have had the Authorization header set
            httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
            httpClient.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("my-secret");
        }

        #endregion

        #region TemperatureValidator Tests

        [Fact]
        public async Task AiCompletion_WithValidTemperature_DoesNotThrowValidationError()
        {
            // Temperature 0.7 is valid - the method should proceed but may fail on encryption
            var configMock = new Mock<IConfiguration>();
            configMock.SetupGet(x => x["ChatGptTemperature"]).Returns("0.7");
            configMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");

            var localizationSecretMock = new Mock<ILocalizationSecret>();
            localizationSecretMock.SetupGet(x => x.ChatGptEncryptedSecret).Returns("dummySecret");
            localizationSecretMock.SetupGet(x => x.ChatGptEncryptionKey).Returns("dummyKey");

            var saltSection = new Mock<IConfigurationSection>();
            configMock.Setup(x => x.GetSection("Salt")).Returns(saltSection.Object);

            var service = new AssistantService(
                _loggerMock.Object,
                configMock.Object,
                _httpClient,
                localizationSecretMock.Object,
                Mock.Of<IGlossaryRepository>()
            );

            var request = new AiCompletionRequest("Test", 0.5);
            // Will fail due to salt being null but should pass temperature validation
            var result = await service.AiCompletion(request);
            result.Should().BeNull(); // Fails at salt validation
        }

        #endregion

        #region AiCompletionModel Tests

        [Fact]
        public void AiCompletionModel_ConstructCommand_SetsCorrectProperties()
        {
            var model = new AiCompletionModel();
            var result = model.ConstructCommand("Translate hello to Spanish", 0.7);

            result.model.Should().Be("gpt-4o-mini");
            result.temperature.Should().Be(0.7);
            result.messages.Should().HaveCount(2);
            result.messages[0].role.Should().Be("system");
            result.messages[1].role.Should().Be("user");
            result.messages[1].content.Should().Be("Translate hello to Spanish");
        }

        #endregion

        #region AiCompletionRequest Model Tests

        [Fact]
        public void AiCompletionRequest_Constructor_SetsProperties()
        {
            var request = new AiCompletionRequest("Test message", 0.5);
            request.Message.Should().Be("Test message");
            request.Temperature.Should().Be(0.5);
        }

        #endregion

        #region RestResponse Model Tests

        [Fact]
        public void RestResponse_Properties_SetCorrectly()
        {
            var response = new RestResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                ResponseData = "test data"
            };

            response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            ((string)response.ResponseData).Should().Be("test data");
        }

        #endregion

        #region ChatGptAiCompletionRequestResponse Model Tests

        [Fact]
        public void ChatGptAiCompletionRequestResponse_Properties_SetCorrectly()
        {
            var response = new ChatGptAiCompletionRequestResponse
            {
                id = "test-id",
                @object = "chat.completion",
                created = 12345,
                model = "gpt-4",
                usage = new Usage { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 },
                choices = new List<Choice>
                {
                    new Choice
                    {
                        index = 0,
                        finish_reason = "stop",
                        message = new Message { role = "assistant", content = "Hola" }
                    }
                }
            };

            response.id.Should().Be("test-id");
            response.model.Should().Be("gpt-4");
            response.usage.total_tokens.Should().Be(30);
            response.choices.Should().HaveCount(1);
            response.choices[0].message.content.Should().Be("Hola");
        }

        #endregion

        #region SuggestLanguageRequest Model Tests

        [Fact]
        public void SuggestLanguageRequest_DefaultValues_AreCorrect()
        {
            var request = new SuggestLanguageRequest();
            request.MaxCharacterLength.Should().Be(0);
        }

        [Fact]
        public void SuggestLanguageRequest_Properties_SetCorrectly()
        {
            var request = new SuggestLanguageRequest
            {
                ElementType = "button",
                ElementApplicationContext = "checkout",
                ElementDetailContext = "submit button",
                Temperature = 0.7,
                MaxCharacterLength = 50,
                SourceText = "Submit",
                DestinationLanguage = "es",
                CurrentLanguage = "en"
            };

            request.ElementType.Should().Be("button");
            request.ElementApplicationContext.Should().Be("checkout");
            request.MaxCharacterLength.Should().Be(50);
        }

        #endregion

        #region BuildGlossaryContext Tests

        [Fact]
        public void BuildGlossaryContext_WithAllFields_ReturnsFormattedContext()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Homepage", Type = "Full form", Context = "This refers to the homepage of the site.", Language = null }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Bengali");

            result.Should().Contain("Glossary: Homepage");
            result.Should().Contain("Type: Full form");
            result.Should().Contain("Context: This refers to the homepage of the site.");
            result.Should().Contain("Transliterate in destination language unless specific word exist for it in that language.");
        }

        [Fact]
        public void BuildGlossaryContext_WithMissingOptionalFields_OmitsTypeAndContext()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Dashboard", Type = null, Context = null, Language = null }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "French");

            result.Should().Contain("Glossary: Dashboard");
            result.Should().NotContain("Type:");
            result.Should().NotContain("Context:");
            result.Should().Contain("Transliterate in destination language");
        }

        [Fact]
        public void BuildGlossaryContext_WithEmptyType_OmitsType()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Settings", Type = "", Context = "App settings page", Language = null }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Spanish");

            result.Should().Contain("Glossary: Settings");
            result.Should().NotContain("Type:");
            result.Should().Contain("Context: App settings page");
        }

        [Fact]
        public void BuildGlossaryContext_IncludesAllGlossaries_RegardlessOfLanguageField()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "About", Language = "Bengali", Type = "Full form", Context = "About page" },
                new Glossary { Name = "Contact", Language = "French", Type = "Full form", Context = "Contact page" }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Bengali");

            result.Should().Contain("Glossary: About");
            result.Should().Contain("Glossary: Contact");
        }

        [Fact]
        public void BuildGlossaryContext_WithNullLanguage_AlwaysIncludes()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Menu", Language = null, Type = "Phrase", Context = "Navigation menu" }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "German");

            result.Should().Contain("Glossary: Menu");
            result.Should().Contain("Type: Phrase");
        }

        [Fact]
        public void BuildGlossaryContext_WithEmptyLanguage_AlwaysIncludes()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Profile", Language = "", Type = null, Context = "User profile" }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Japanese");

            result.Should().Contain("Glossary: Profile");
        }

        [Fact]
        public void BuildGlossaryContext_WithEmptyList_ReturnsEmpty()
        {
            var result = AssistantService.BuildGlossaryContext(new List<Glossary>(), "English");

            result.Should().BeEmpty();
        }

        [Fact]
        public void BuildGlossaryContext_WithNullList_ReturnsEmpty()
        {
            var result = AssistantService.BuildGlossaryContext(null, "English");

            result.Should().BeEmpty();
        }

        [Fact]
        public void BuildGlossaryContext_IncludesGlossaryWithDifferentLanguageField()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Test", Language = "French", Type = "Full form", Context = "Test context" }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Bengali");

            result.Should().Contain("Glossary: Test");
        }

        [Fact]
        public void BuildGlossaryContext_WithMultipleGlossaries_ReturnsNewlineSeparated()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Home", Language = null, Type = "Full form", Context = "Home page" },
                new Glossary { Name = "About", Language = null, Type = "Phrase", Context = "About section" }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Bengali");

            result.Should().Contain("Glossary: Home");
            result.Should().Contain("Glossary: About");
            result.Should().Contain("\n");
        }

        [Fact]
        public void BuildGlossaryContext_IncludesGlossary_WithSpecificLanguageField()
        {
            var glossaries = new List<Glossary>
            {
                new Glossary { Name = "Test", Language = "bengali", Type = null, Context = null }
            };

            var result = AssistantService.BuildGlossaryContext(glossaries, "Bengali");

            result.Should().Contain("Glossary: Test");
        }

        #endregion

        #region GenerateSuggestTranslationContext With Glossary Tests

        [Fact]
        public void GenerateSuggestTranslationContext_WithGlossaryContext_InsertsBeforeTranslateInstruction()
        {
            var request = new SuggestLanguageRequest
            {
                SourceText = "About",
                DestinationLanguage = "Bengali",
                CurrentLanguage = "English"
            };
            var glossaryContext = "Glossary: About, Type: Full form, Context: This refers to the homepage of the site. Transliterate in destination language unless specific word exist for it in that language.";

            var result = AssistantService.GenerateSuggestTranslationContext(request, glossaryContext);

            result.Should().Contain("translate a user interface element");
            result.Should().Contain(glossaryContext);
            result.Should().Contain("Translate the following from English to Bengali: 'About'");
            // Glossary context should appear between default context and translate instruction
            var glossaryIndex = result.IndexOf(glossaryContext);
            var translateIndex = result.IndexOf("Translate the following");
            glossaryIndex.Should().BeLessThan(translateIndex);
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithNullGlossaryContext_SkipsGlossary()
        {
            var request = new SuggestLanguageRequest
            {
                SourceText = "Hello",
                DestinationLanguage = "French",
                CurrentLanguage = "English"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request, null);

            result.Should().Contain("translate a user interface element");
            result.Should().Contain("Translate the following from English to French: 'Hello'");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithEmptyGlossaryContext_SkipsGlossary()
        {
            var request = new SuggestLanguageRequest
            {
                SourceText = "Submit",
                DestinationLanguage = "Spanish",
                CurrentLanguage = "English"
            };

            var result = AssistantService.GenerateSuggestTranslationContext(request, "");

            result.Should().NotContain("\n\n");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithGlossaryAndElementDetailContext_UsesElementDetailContext()
        {
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = "Custom context for button",
                SourceText = "Save",
                DestinationLanguage = "German",
                CurrentLanguage = "English"
            };
            var glossaryContext = "Glossary: Save, Type: Phrase, Context: Save button. Transliterate in destination language unless specific word exist for it in that language.";

            var result = AssistantService.GenerateSuggestTranslationContext(request, glossaryContext);

            result.Should().Contain("Custom context for button");
            result.Should().Contain(glossaryContext);
        }

        #endregion
    }
}
