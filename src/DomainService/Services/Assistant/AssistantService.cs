using DomainService.Repositories;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly.CircuitBreaker;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace DomainService.Services
{
    public class AssistantService : IAssistantService
    {
        private readonly ILogger<AssistantService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _aiCompletionUrl;
        private readonly string _chatGptTemperature;
        private readonly HttpClient _httpClient;
        private readonly ILocalizationSecret _localizationSecret;
        private readonly IGlossaryRepository _glossaryRepository;
        public AssistantService(
            ILogger<AssistantService> logger,
            IConfiguration configuration,
            HttpClient httpClient,
            ILocalizationSecret localizationSecret,
            IGlossaryRepository glossaryRepository
        )
        {
            _localizationSecret = localizationSecret;
            _logger = logger;
            _configuration = configuration;
            _aiCompletionUrl = _configuration["AiCompletionUrl"];
            _chatGptTemperature = _configuration["ChatGptTemperature"];
            _httpClient = httpClient;
            _glossaryRepository = glossaryRepository;
        }


        public async Task<string> SuggestTranslation(SuggestLanguageRequest query)
        {
            string glossaryContext = null;
            if (query.GlossaryIds != null && query.GlossaryIds.Any())
            {
                var glossaries = await _glossaryRepository.GetByIdsAsync(query.GlossaryIds);
                glossaryContext = BuildGlossaryContext(glossaries, query.DestinationLanguage);
            }

            var context = GenerateSuggestTranslationContext(query, glossaryContext);

            var aiCompletionRequest = new AiCompletionRequest(context, query.Temperature);

            var aiText = await AiCompletion(aiCompletionRequest);
            // var aiText = await CallAIAgent("", context);  

            var maxRetryCount = 3;
            var retryCount = 0;

            while (string.IsNullOrEmpty(aiText) && retryCount < maxRetryCount)
            {
                await Task.Delay(5000);

                aiText = await AiCompletion(aiCompletionRequest);
                retryCount++;
            }
            if (retryCount >= maxRetryCount)
            {
                _logger.LogError("SuggestTranslation -> CallAiCompletion: Maximum Retry count reached");
                return null;
            }

            var output = FormatAiTextForSuggestTranslation(aiText);
            return output;
        }

        public static string GenerateSuggestTranslationContext(SuggestLanguageRequest request, string? glossaryContext = null)
        {
            var context = !string.IsNullOrWhiteSpace(request.ElementDetailContext) ? request.ElementDetailContext :
                $"The requirement is to translate a user interface element of a webpage. Output only the translated text (no quotes, no explanation).";
            if (!string.IsNullOrWhiteSpace(glossaryContext))
            {
                context += $"\n{glossaryContext}\n";
            }
            context += $"Translate the following from {request.CurrentLanguage} to {request.DestinationLanguage}: '{request.SourceText}'";
            return context;
        }

        public static string BuildGlossaryContext(List<Glossary> glossaries, string targetLanguage)
        {
            if (glossaries == null || !glossaries.Any())
                return string.Empty;

            var glossaryLines = new List<string>();
            foreach (var glossary in glossaries)
            {
                var line = $"Glossary: {glossary.Name}";
                if (!string.IsNullOrWhiteSpace(glossary.Type))
                    line += $", Type: {glossary.Type}";
                if (!string.IsNullOrWhiteSpace(glossary.Context))
                    line += $", Context: {glossary.Context}";
                //line += ". Transliterate in destination language unless specific word exist for it in that language.";
                glossaryLines.Add(line);
            }

            return string.Join("\n", glossaryLines);
        }

        public static string FormatAiTextForSuggestTranslation(string aiText)
        {
            if (string.IsNullOrWhiteSpace(aiText))
            {
                return string.Empty;
            }

            string output = null;

            var trimmedAiText = aiText?.Replace("\"", "").Replace("'", "");
            if (!string.IsNullOrEmpty(trimmedAiText) && trimmedAiText.Contains(":"))
            {
                string[] parts = trimmedAiText.Split(':');
                output = parts.Length > 1 ? parts[1] : trimmedAiText;
            }
            else
            {
                output = trimmedAiText;
            }

            char[] charsToTrim = { ' ', '\t', '\n' };
            string trimmedOutput = output?.Trim(charsToTrim) ?? string.Empty;

            return trimmedOutput;
        }

        public async Task<string> AiCompletion(AiCompletionRequest request)
        {
            try
            {
                double.TryParse(_chatGptTemperature, out var temperature);
                TemperatureValidator(temperature);

                var encryptedSecret = await GetEncryptedSecret();
                if (string.IsNullOrEmpty(encryptedSecret))
                {
                    throw new ArgumentException("Get null value from MicroserviceConfig");
                }

                var secret = GetDecryptedSecret(encryptedSecret);

                var model = new AiCompletionModel();
                var payload = model.ConstructCommand(request.Message, request.Temperature);

                var httpRequest = PrepareHttpRequest(_aiCompletionUrl, HttpMethod.Post, JsonConvert.SerializeObject(payload));
                var httpResponse = await MakeRequestAsync(httpRequest, secret);

                if (httpResponse != null && httpResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    ChatGptAiCompletionRequestResponse respone = JsonConvert.DeserializeObject<ChatGptAiCompletionRequestResponse>(httpResponse.ResponseData);
                    var responeMessage = respone.choices?.FirstOrDefault()?.message?.content;
                    return responeMessage;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AiCompletionCommandHandler: Exception occurred");
            }

            return null;
        }

        private async Task<string> GetEncryptedSecret()
        {
            return _localizationSecret.ChatGptEncryptedSecret;
        }
 
        private string GetDecryptedSecret(string encryptedText)
        {
            var key = _localizationSecret.ChatGptEncryptionKey;
            var salt = GetSalt();
            
            if (salt is null)
            {
                throw new ArgumentException("Salt is null");
            }

            var decryptedValue = Decrypt(encryptedText, key, salt);
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

        public static HttpRequestMessage PrepareHttpRequest(string requestUrl, HttpMethod httpRequestType, object content = null)
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = httpRequestType,
                RequestUri = new Uri(requestUrl)
            };

            if (content != null)
            {
                var jsonContent = new StringContent((string)content, Encoding.UTF8, "application/json");

                httpRequestMessage.Content = jsonContent;
            }

            return httpRequestMessage;
        }

        public async Task<RestResponse> MakeRequestAsync(HttpRequestMessage httpRequestMessage, string secret)
        {
            var response = new RestResponse();
            var requestResponse = new HttpResponseMessage();

            try
            {
                _logger.LogInformation("Started processing the API request. MethodType: {MethodType}, BaseUrl: {BaseUrl}, ApiName: {ApiName}",
                    httpRequestMessage.Method, _httpClient.BaseAddress, httpRequestMessage.RequestUri);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);

                requestResponse = await _httpClient.SendAsync(httpRequestMessage);

                response.HttpStatusCode = requestResponse.StatusCode;
                response.ResponseData = await requestResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Completed processing the API request. MethodType: {MethodType}, BaseUrl: {BaseUrl}, ApiName: {ApiName}, SerializedResponse: {SerializedResponse}",
                    httpRequestMessage.Method, _httpClient.BaseAddress, httpRequestMessage.RequestUri, JsonConvert.SerializeObject(response));
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker Exception occurred while processing the API request. MethodType: {MethodType}, BaseUrl: {BaseUrl}, ApiName: {ApiName}, Reason: {Reason}",
                    httpRequestMessage.Method, _httpClient.BaseAddress, httpRequestMessage.RequestUri, ex.Message);
                throw;
            }

            return response;
        }

        private async Task<string> CallAIAgent(string projectKey, string message, string widgetId = "")
        {
            try
            {
                var apiBaseUrl = _configuration["ApiBaseUrl"];

                // 1. Initiate
                var initiateUrl = $"{apiBaseUrl}/blocksai-api/v1/conversation/initiate?widget_id={widgetId}";
                var request = new HttpRequestMessage(HttpMethod.Get, initiateUrl);
                request.Headers.Add("X-Blocks-Key", projectKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var initiateJson = await response.Content.ReadAsStringAsync();
                var initiateData = JsonConvert.DeserializeObject<Dictionary<string, string>>(initiateJson)
                    ?? throw new Exception("Invalid initiate response");

                var websocketPath = initiateData["websocket_url"];
                var token = initiateData["token"];

                if (string.IsNullOrEmpty(websocketPath) || string.IsNullOrEmpty(token))
                    throw new Exception("Missing websocket_url or token");

                // 2. WebSocket connect
                var wsUrl =
                    $"{apiBaseUrl}/blocksai-api/v1{websocketPath}" +
                    $"?token={token}&x_blocks_key={projectKey}&pg=true&send_event=true";

                wsUrl = wsUrl.Replace("https://", "wss://").Replace("http://", "ws://");

                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

                // 3. Send user message
                var payload = JsonConvert.SerializeObject(new { message });
                var bytes = Encoding.UTF8.GetBytes(payload);

                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                // 4. Receive events
                var buffer = new byte[16 * 1024];

                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Dictionary<string, object> evt;
                    try
                    {
                        evt = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    }
                    catch
                    {
                        continue;
                    }

                    if (evt == null || !evt.TryGetValue("type", out var typeVal))
                        continue;

                    var type = typeVal?.ToString();

                    // 5. Final response event - Return only the message content
                    if (type == "chat_response" && evt.TryGetValue("message", out var msgVal))
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                        return msgVal?.ToString();
                    }

                    // other events: typing, tool_call, status, etc → ignore
                }

                throw new Exception("chat_response event not received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CallAIAgent: Exception occurred");
                return null;
            }
        }
    }
}
