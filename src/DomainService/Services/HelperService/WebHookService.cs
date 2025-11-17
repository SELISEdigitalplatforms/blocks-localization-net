using DomainService.Repositories;
using DomainService.Shared;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Logging;

namespace DomainService.Services.HelperService
{
    public class WebHookService : IWebHookService
    {
        private readonly IBlocksWebhookRepository _blocksWebhookRepository;
        private readonly IHttpHelperServices _httpHelperServices;
        private readonly ILogger<WebHookService> _logger;

        public WebHookService(IBlocksWebhookRepository blocksWebhookRepository, IHttpHelperServices httpHelperServices, ILogger<WebHookService> logger)
        {
            _blocksWebhookRepository = blocksWebhookRepository;
            _httpHelperServices = httpHelperServices;
            _logger = logger;
        }

        public async Task<bool> CallWebhook(object payload)
        {
            var webhook = await _blocksWebhookRepository.GetAsync();
            if (webhook == null || webhook.IsDisabled) return true;

            return await _httpHelperServices.MakeHttpRequestForWebhook(payload, webhook);
        }

        public async Task<ApiResponse> SaveWebhookAsync(BlocksWebhook webhook)
        {
            //var validationResult = await _validator.ValidateAsync(webhook);

            //if (!validationResult.IsValid)
            //    return new ApiResponse(string.Empty, validationResult.Errors);

            try
            {
                //var repoModule = await MappedIntoRepoModuleAsync(webhook);
                await _blocksWebhookRepository.SaveAsync(webhook);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while saving BlocksWebhook {errorMessage} : {StackTrace}", ex.Message, ex.StackTrace);
                return new ApiResponse(ex.Message);
            }

            return new ApiResponse();
        }
    }
}