using DomainService.Repositories;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DomainService.Services
{
    public class JsonOutputGeneratorService : OutputGenerator
    {
        private readonly ILogger<XlsxOutputGeneratorService> _logger;

        public JsonOutputGeneratorService()
        {

        }
        public JsonOutputGeneratorService(ILogger<XlsxOutputGeneratorService> logger)
        {
            _logger = logger;
        }
        public override Task<T> GenerateAsync<T>(BlocksLanguage languageSetting, List<BlocksLanguageModule> applications,
            List<BlocksLanguageKey> resourceKeys, string defaultLanguage)
        {
            try
            {
                var identifiers = new string[] { languageSetting.LanguageCode };

                var jsonOutputModels = new List<LanguageJsonModel>();

                foreach (BlocksLanguageKey resourceKey in resourceKeys)
                {
                    BlocksLanguageModule app = applications.FirstOrDefault(x => x.ItemId == resourceKey.ModuleId);

                    var model = new LanguageJsonModel
                    {
                        _id = resourceKey.ItemId,
                        ModuleId = resourceKey.ModuleId,
                        Value = resourceKey.Value,
                        KeyName = resourceKey.KeyName,
                        Resources = resourceKey.Resources.Where(x => identifiers.Contains(x.Culture)).ToArray(),
                        TenantId = resourceKey.TenantId,
                        IsPartiallyTranslated = resourceKey.IsPartiallyTranslated,
                        Routes = resourceKey.Routes
                    };

                    jsonOutputModels.Add(model);
                }
                string jsonString = JsonConvert.SerializeObject(jsonOutputModels, Formatting.Indented);
                return Task.FromResult((T)(object)jsonString);
            }
            catch (Exception ex)
            {
                _logger.LogError("JsonOutputGeneratorService: GenerateAsync: Error: {ExMessage}", ex.Message);
                return Task.FromResult((T)(object)null);
            }
        }
    }
}