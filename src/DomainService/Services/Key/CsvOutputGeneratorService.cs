using CsvHelper;
using DomainService.Repositories;
using DomainService.Shared.Entities;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace DomainService.Services
{
    public class CsvOutputGeneratorService : OutputGenerator
    {
        private readonly ILogger<CsvOutputGeneratorService> _logger;

        public CsvOutputGeneratorService()
        {

        }

        public CsvOutputGeneratorService(ILogger<CsvOutputGeneratorService> logger)
        {
            _logger = logger;
        }

        public override Task<T> GenerateAsync<T>(BlocksLanguage languageSetting, List<BlocksLanguageModule> applications, List<BlocksLanguageKey> resourceKeys, string defaultLanguage)
        {
            try
            {
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
                        Resources = resourceKey.Resources,
                        TenantId = resourceKey.TenantId,
                        IsPartiallyTranslated = resourceKey.IsPartiallyTranslated,
                        Routes = resourceKey.Routes
                    };

                    jsonOutputModels.Add(model);
                }

                var identifiers = new string[] { languageSetting.LanguageCode };

                var builder = new StringBuilder();
                var stringWriter = new StringWriter(builder);
                var csv = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);

                csv.WriteField("Id");
                csv.WriteField("AppId");
                csv.WriteField("Type");
                csv.WriteField("App");
                csv.WriteField("Module");
                csv.WriteField("Key");

                foreach (string identifier in identifiers)
                {
                    csv.WriteField(identifier);
                    if (identifier != defaultLanguage)
                    {
                        csv.WriteField(identifier + "_CharacterLength");
                    }
                }

                csv.NextRecord();

                foreach (var item in jsonOutputModels)
                {
                    csv.WriteField(item._id);
                    csv.WriteField(item.TenantId);
                    csv.WriteField(item.Value);
                    csv.WriteField(item.KeyName);
                    csv.WriteField(item.ModuleId);
                    csv.WriteField(item.Routes);
                    csv.WriteField(item.Resources);
                    csv.WriteField(item.IsPartiallyTranslated);

                    foreach (string identifier in identifiers)
                    {
                        var resourceKey = item.Resources.FirstOrDefault(x => x.Culture == identifier);
                        var resourceValue = resourceKey?.Value;
                        csv.WriteField(resourceValue);

                        if (identifier != defaultLanguage)
                        {
                            csv.WriteField(resourceKey?.CharacterLength);
                        }
                    }

                    csv.NextRecord();
                }

                var csvData = builder.ToString();
                var bytes = Encoding.UTF8.GetBytes(csvData);
                var stream = new MemoryStream(bytes);

                return Task.FromResult((T)(object)stream);
            }
            catch (Exception ex)
            {
                _logger.LogError("CsvOutputGeneratorService: GenerateAsync: Error: {ExMessage}", ex.Message);
                return Task.FromResult((T)(object)null);
            }
        }
    }
}

