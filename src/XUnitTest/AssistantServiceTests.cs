using DomainService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

public class AssistantServiceTests
{
    private readonly Mock<ILogger<AssistantService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly AssistantService _assistantService;

    public AssistantServiceTests()
    {
        _loggerMock = new Mock<ILogger<AssistantService>>();
        _configurationMock = new Mock<IConfiguration>();
        _httpClientMock = new Mock<HttpClient>();

        _configurationMock.SetupGet(x => x["Key"]).Returns("test-key");
        _configurationMock.SetupGet(x => x["AiCompletionUrl"]).Returns("http://test-url.com");
        _configurationMock.SetupGet(x => x["ChatGptTemperature"]).Returns("0.7");

        _assistantService = new AssistantService(
            _loggerMock.Object,
            _configurationMock.Object,
            _httpClientMock.Object
        );
    }


    [Fact]
    public void GenerateSuggestTranslationContext_ShouldReturnCorrectContext()
    {
        // Arrange
        var request = new SuggestLanguageRequest
        {
            ElementType = "button",
            ElementApplicationContext = "login",
            ElementDetailContext = "submit",
            MaxCharacterLength = 50,
            SourceText = "Submit",
            DestinationLanguage = "es",
            CurrentLanguage = "en"
        };

        // Act
        var result = _assistantService.GenerateSuggestTranslationContext(request);

        // Assert
        Assert.Contains("The requirement is to translate a user interface element of a webpage.", result);
        Assert.Contains("Ideally,it should not exceed 50 Characters.", result);
        Assert.Contains("The element type in question is 'button'.", result);
        Assert.Contains("The element application context in question is 'login'.", result);
        Assert.Contains("The element detail context in question is: 'submit'.", result);
        Assert.Contains("translate the following from en to es:'Submit'.", result);
    }

    [Fact]
    public void FormatAiTextForSuggestTranslation_ShouldReturnFormattedText()
    {
        // Arrange
        var aiText = "\"Translated: Enviar\"";

        // Act
        var result = _assistantService.FormatAiTextForSuggestTranslation(aiText);

        // Assert
        Assert.Equal("Enviar", result);
    }
}
