using Microsoft.Extensions.Logging;
using MongoDB.Bson.IO;
using System.Net;

namespace DomainService.Services.Assistant
{
    public class AssistantService
    {
        private readonly ILogger<AssistantService> _logger;
        public AssistantService(
            ILogger<AssistantService> logger
        ) 
        { 
            _logger = logger;
        }

        public async Task<string> HandleAsync(AiCompletionRequest request)
        {

            try
            {
                double.TryParse("0.1", out var temperature);
                TemperatureValidator(temperature);

                var encryptedSecret = await GetEncryptedSecretFromMicroServiceConfig();
                if (string.IsNullOrEmpty(encryptedSecret))
                {
                    throw new ArgumentException("Get null value from MicroserviceConfig");
                }

                var secret = GetDecryptedSecret(encryptedSecret);
                var identityTokenResponse = new IdentityTokenResponse
                {
                    TokenType = "Bearer",
                    AccessToken = secret
                };

                var model = new AiCompletionModel();
                var payload = model.ConstructCommand(request.Message, request.Temperature);

                var httpRequest = HttpRequestHelper.PrepareHttpRequest(_aiCompletionUrl, HttpMethod.Post, JsonConvert.SerializeObject(payload));
                var httpResponse = await _blocksAssistant.MakeHttpCall(httpRequest, identityTokenResponse);

                if (httpResponse != null && httpResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    ChatGptAiCompletionRequestResponse respone = JsonConvert.DeserializeObject<ChatGptAiCompletionRequestResponse>(httpResponse.ResponseData);
                    var responeMessage = respone.choices?.FirstOrDefault()?.message?.content;
                    return responeMessage;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"AiCompletionCommandHandler: {ex}");
            }

            return null;
        }

        private static void TemperatureValidator(double temperature)
        {
            if (temperature < 0 || temperature > 1)
            {
                throw new ArgumentException("Invalid Temperature Value");
            }
        }
    }
}
