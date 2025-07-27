using Blocks.Extension.DependencyInjection;
using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Services.HelperService;
using DomainService.Shared.Events;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Storage.DomainService.Storage;
using Storage.DomainService.Storage.Validators;
using Worker.Consumers;


namespace Worker
{
    public static class ServiceRegistry
    {
        public static void RegisterApplicationServices(this IServiceCollection services)
        {

            services.AddSingleton<IConsumer<GenerateUilmFilesEvent>, GenerateUilmFilesConsumer>();
            services.AddSingleton<IConsumer<TranslateAllEvent>, TranslateAllEventConsumer>();
            services.AddSingleton<IConsumer<UilmImportEvent>, UilmImportEventConsumer>();
            services.AddSingleton<IConsumer<UilmExportEvent>, UilmExportEventConsumer>();

            services.AddSingleton<XlsxOutputGeneratorService>();
            services.AddSingleton<JsonOutputGeneratorService>();
            services.AddSingleton<CsvOutputGeneratorService>();

            services.AddSingleton<IModuleManagementService, ModuleManagementService>();
            services.AddSingleton<IModuleRepository, ModuleRepository>();
            services.AddSingleton<IValidator<Module>, ModuleValidator>();

            services.AddSingleton<ILanguageManagementService, LanguageManagementService>();
            services.AddSingleton<ILanguageRepository, LanguageRepository>();
            services.AddSingleton<IValidator<Language>, LanguageValidator>();
            
            services.AddSingleton<StorageHelper>();

            services.AddSingleton<IKeyManagementService, KeyManagementService>();
            services.AddSingleton<IKeyRepository, KeyRepository>();
            services.AddSingleton<IValidator<Key>, KeyValidator>();

            services.AddSingleton<IAssistantService, AssistantService>();

            services.RegisterBlocksStorageServices();
            services.AddTransient<IValidator<UpdateFileRequest>, UpdateFileRequestValidator>();
        }
    }
}
