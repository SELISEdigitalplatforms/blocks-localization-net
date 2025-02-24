using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Events;
using FluentValidation;
using Worker.Consumers;


namespace Worker
{
    public static class ServiceRegistry
    {
        public static void RegisterApplicationServices(this IServiceCollection services)
        {


            services.AddSingleton<IConsumer<GenerateUilmFilesEvent>, GenerateUilmFilesConsumer>();

            services.AddSingleton<IKeyManagementService, KeyManagementService>();
            services.AddSingleton<IKeyRepository, KeyRepository>();
            services.AddSingleton<IValidator<Key>, KeyValidator>();

            services.AddSingleton<IAssistantService, AssistantService>();
        }
    }
}
