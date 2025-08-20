using Blocks.Genesis;
using DomainService.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace DomainService.Services.HelperService
{
    public class NotificationService : INotificationService
    {
        private readonly ICryptoService _cryptoService;
        private readonly ITenants _tenants;
        private readonly IConfiguration _configuration;
        private readonly IHttpHelperServices _httpHelperServices;
        public NotificationService(
                                   ICryptoService cryptoService,
                                   ITenants tenants,
                                   IConfiguration configuration,
                                   IHttpHelperServices httpHelperServices)
        {
            _cryptoService = cryptoService;
            _tenants = tenants;
            _configuration = configuration;
            _httpHelperServices = httpHelperServices;
        }

        public async Task<bool> NotifyExportEvent(bool response, string fileId, string? messageCoRelationId, string tenantId)
        {
            //Console.WriteLine($"Notifcation Service : {UserId} -- {TenantId}");

            var requestData = new
            {
                ConnectionId = messageCoRelationId,
                Roles = new List<string> { },
                UserIds = new List<string> { BlocksContext.GetContext()?.UserId ?? "" },
                DenormalizedPayload = JsonSerializer.Serialize(new
                {
                    Message = new
                    {
                        IsSuccess = response,
                        title = "Uilm Export Completed",
                        description = "Completed export"
                    }
                }),
                SaveDenormalizedPayloadAsAnObject = false,
                ConfiguratoinName = _configuration["BlocksAppNotificationReceiver"],
                ContentAvailable = true,
                ResponseKey = messageCoRelationId,
                ResponseValue = response.ToString()
            };

            var blocksKey = _configuration["RootTenantId"];
            var rootTenantId = _configuration["RootTenantId"];
            var salt = _tenants.GetTenantByID(rootTenantId)?.TenantSalt;
            var actulalSecret = _cryptoService.Hash(rootTenantId, salt);

            var url = _configuration["NotificationServiceUrl"];
            var headers = new Dictionary<string, string>
            {
                { "x-blocks-key", blocksKey },
                { "Secret", actulalSecret}
            };

            var (result1, result2) = await _httpHelperServices.MakeHttpPostRequest<NotificationResponse>(
                 requestData, url, headers);

            return result1 == null ? false : result1.isSuccess;
        }

        public async Task<bool> NotifyTranslateAllEvent(bool response, string? messageCoRelationId)
        {
            var requestData = new
            {
                ConnectionId = messageCoRelationId,
                Roles = new List<string> { },
                UserIds = new List<string> { BlocksContext.GetContext()?.UserId ?? "" },
                DenormalizedPayload = JsonSerializer.Serialize(new
                {
                    IsSuccess = response,
                    title = "Translation Completed",
                    description = "Completed translation for all keys"
                }),
                SaveDenormalizedPayloadAsAnObject = false,
                ConfiguratoinName = "translate-all",
                ContentAvailable = true,
                ResponseKey = "Translate All",
                ResponseValue = "Successfully translated all keys"
            };

            var blocksKey = _configuration["RootTenantId"];
            var rootTenantId = _configuration["RootTenantId"];
            var salt = _tenants.GetTenantByID(rootTenantId)?.TenantSalt;
            var actulalSecret = _cryptoService.Hash(rootTenantId, salt);

            var url = _configuration["NotificationServiceUrl"];
            var headers = new Dictionary<string, string>
            {
                { "x-blocks-key", blocksKey },
                { "Secret", actulalSecret}
            };

            var (result1, result2) = await _httpHelperServices.MakeHttpPostRequest<NotificationResponse>(
                 requestData, url, headers);

            return result1 == null ? false : result1.isSuccess;
        }
    }
}
