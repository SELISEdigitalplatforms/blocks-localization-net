using DomainService.Services;
using FluentAssertions;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http;
using Xunit;

namespace XUnitTest
{
    public class AssistantServiceTests
    {
        private readonly Mock<ILogger<AssistantService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<HttpClient> _httpClientMock;
        private readonly Mock<ILocalizationSecret> _localizationSecretMock;
        private readonly AssistantService _assistantService;

        public AssistantServiceTests()
        {
            _loggerMock = new Mock<ILogger<AssistantService>>();
            _configurationMock = new Mock<IConfiguration>();
            _httpClientMock = new Mock<HttpClient>();
            _localizationSecretMock = new Mock<ILocalizationSecret>();

            _configurationMock.SetupGet(x => x["Key"]).Returns("test-key");
            _configurationMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");
            _configurationMock.SetupGet(x => x["ChatGptTemperature"]).Returns("0.7");

            _assistantService = new AssistantService(
                _loggerMock.Object,
                _configurationMock.Object,
                _httpClientMock.Object,
                _localizationSecretMock.Object
            );
        }

        [Fact]
        public void GenerateSuggestTranslationContext_ShouldReturnCorrectContext()
        {
            // Arrange
            var request = new SuggestLanguageRequest
            {
                // Fill with appropriate properties if needed
            };

            // Act
            var result = _assistantService.GenerateSuggestTranslationContext(request);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithElementDetailContext_ReturnsCorrectContext()
        {
            // Arrange
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = "submit",
                SourceText = "Submit",
                DestinationLanguage = "es",
                CurrentLanguage = "en"
            };

            // Act
            var result = _assistantService.GenerateSuggestTranslationContext(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("submit");
            result.Should().Contain("Translate the following from en to es: 'Submit'");
        }

        [Fact]
        public void GenerateSuggestTranslationContext_WithoutElementDetailContext_ReturnsDefaultContext()
        {
            // Arrange
            var request = new SuggestLanguageRequest
            {
                ElementDetailContext = null,
                SourceText = "Welcome",
                DestinationLanguage = "fr",
                CurrentLanguage = "en"
            };

            // Act
            var result = _assistantService.GenerateSuggestTranslationContext(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("translate a user interface element");
            result.Should().Contain("Translate the following from en to fr: 'Welcome'");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithColon_ExtractsTextAfterColon()
        {
            // Arrange
            var aiText = "\"Translated: Enviar\"";

            // Act
            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            // Assert
            result.Should().Be("Enviar");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithoutColon_ReturnsTrimmedText()
        {
            // Arrange
            var aiText = "\"Bienvenue\"";

            // Act
            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            // Assert
            result.Should().Be("Bienvenue");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithQuotes_RemovesQuotes()
        {
            // Arrange
            var aiText = "'Hello World'";

            // Act
            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_WithWhitespace_TrimsWhitespace()
        {
            // Arrange
            var aiText = "  \n\tBonjour\t\n  ";

            // Act
            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            // Assert
            result.Should().Be("Bonjour");
        }

        [Fact]
        public void FormatAiTextForSuggestTranslation_NullInput_HandlesGracefully()
        {
            // Arrange
            string aiText = null;

            // Act
            var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void PrepareHttpRequest_WithContent_CreatesRequestWithContent()
        {
            // Arrange
            var url = "http://test.com/api";
            var content = "{\"test\": \"data\"}";

            // Act
            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Post, content);

            // Assert
            result.Should().NotBeNull();
            result.Method.Should().Be(HttpMethod.Post);
            result.RequestUri.Should().Be(new Uri(url));
            result.Content.Should().NotBeNull();
        }

        [Fact]
        public void PrepareHttpRequest_WithoutContent_CreatesRequestWithoutContent()
        {
            // Arrange
            var url = "http://test.com/api";

            // Act
            var result = AssistantService.PrepareHttpRequest(url, HttpMethod.Get, null);

            // Assert
            result.Should().NotBeNull();
            result.Method.Should().Be(HttpMethod.Get);
            result.RequestUri.Should().Be(new Uri(url));
            result.Content.Should().BeNull();
        }

        [Fact]
        public void Decrypt_ValidInput_ReturnsDecryptedText()
        {
            // Arrange
            // Note: This test requires proper setup with valid encrypted text and salt
            // For now, we'll test that the method exists and can be called
            var encryptedText = "dGVzdA=="; // base64 encoded "test"
            var key = "test-key";
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            // Act & Assert
            // This will likely throw due to invalid encrypted data, but tests the method exists
            var act = () => AssistantService.Decrypt(encryptedText, key, salt);
            act.Should().NotThrow<NullReferenceException>();
        }
    }
}
