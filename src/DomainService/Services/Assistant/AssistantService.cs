using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;

namespace DomainService.Services.Assistant
{
    public class AssistantService
    {
        private readonly ILogger<AssistantService> _logger;
        private readonly IConfiguration _configuration;
        public AssistantService(
            ILogger<AssistantService> logger,
            IConfiguration configuration
        ) 
        { 
            _logger = logger;
            _configuration = configuration;
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

        private async Task<string> GetEncryptedSecretFromMicroServiceConfig()
        {
            var config = await _blocksAssistant.GetMicroServiceConfig(x => true);
            return config?.ChatGptSecretKey;
        }

        private string GetDecryptedSecret(string encryptedText)
        {
            var salt = GetSalt();
            if (salt is null)
            {
                throw new ArgumentException("Salt is null");
            }

            var decryptedValue = Decrypt(encryptedText, _key, salt);
            return decryptedValue;
        }

        public byte[] GetSalt() =>
            _configuration.GetSection("Salt").Get<byte[]>();

        private static void TemperatureValidator(double temperature)
        {
            if (temperature < 0 || temperature > 1)
            {
                throw new ArgumentException("Invalid Temperature Value");
            }
        }

        public static string Decrypt(string encryptedText, string key, byte[] salt)
        {
            var cipherText = Convert.FromBase64String(encryptedText);

            using (var aesAlg = Aes.Create())
            {
                var keyDerivationFunction = new Rfc2898DeriveBytes(key, salt);
                aesAlg.Key = keyDerivationFunction.GetBytes(aesAlg.KeySize / 8);
                aesAlg.IV = keyDerivationFunction.GetBytes(aesAlg.BlockSize / 8);

                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                string decryptedText;
                using (var msDecrypt = new System.IO.MemoryStream(cipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
                        {
                            decryptedText = srDecrypt.ReadToEnd();
                        }
                    }
                }

                return decryptedText;
            }
        }
    }
}
