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

        public AssistantServiceTests()
        {
            _loggerMock = new Mock<ILogger<AssistantService>>();
            _configurationMock = new Mock<IConfiguration>();
            _localizationSecretMock = new Mock<ILocalizationSecret>();

            _configurationMock.SetupGet(x => x["Key"]).Returns("test-key");
            _configurationMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");
            _configurationMock.SetupGet(x => x["ChatGptTemperature"]).Returns("0.7");

            _localizationSecretMock.SetupGet(x => x.ChatGptEncryptionKey).Returns("dummy-encryption-key");
            _localizationSecretMock.SetupGet(x => x.ChatGptEncryptionSalt)
                .Returns("[\"01\",\"02\",\"03\",\"04\",\"05\",\"06\",\"07\",\"08\"]");

            // Use a stubbed HttpMessageHandler so no real HTTP is performed.
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            _httpClient = new HttpClient(handlerMock.Object);

            _assistantService = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClient,
                _localizationSecretMock.Object
            );
        }

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

            var result = _assistantService.GenerateSuggestTranslationContext(request);

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

            var result = _assistantService.GenerateSuggestTranslationContext(request);

            result.Should().Contain("translate a user interface element", because: "default context should be used");
            result.Should().Contain("Translate the following from en to fr: 'Welcome'");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithColon_ExtractsTextAfterColon()
        {
            var aiText = "\"Translated: Enviar\"";

            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Enviar");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithoutColon_ReturnsTrimmedText()
        {
            var aiText = "\"Bienvenue\"";

            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Bienvenue");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithQuotes_RemovesQuotes()
        {
            var aiText = "'Hello World'";

            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Hello World");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithWhitespace_TrimsWhitespace()
        {
            var aiText = "  \n\tBonjour\t\n  ";

            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().Be("Bonjour");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_NullInput_ReturnsEmpty()
        {
            string? aiText = null;

            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            result.Should().BeEmpty();
        }

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

        [Fact]
        public void Decrypt_WithInvalidCipher_ThrowsCryptographicException()
        {
            var encryptedText = "dGVzdA=="; // base64 of "test", not a valid AES cipher for this key/salt
            var key = "test-key";
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            Action act = () => AssistantService.Decrypt(encryptedText, key, salt);

            act.Should().Throw<Exception>(); // cryptographic failure is expected, but should not cause null refs
        }
    }
}
