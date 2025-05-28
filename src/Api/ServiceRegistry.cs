using DomainService.Repositories;
using DomainService.Services;
using FluentValidation;

namespace Api
{
    /// <summary>
    /// A static class responsible for registering application services and validators.
    /// </summary>
    
    public static class ServiceRegistry
    {
        /// <summary>
        /// Registers services and validators related to modules, languages, and keys.
        /// </summary>
        /// <param name="services">The collection of services to which dependencies are registered.</param>
        
        public static void RegisterApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<IModuleManagementService, ModuleManagementService>();
            services.AddSingleton<IModuleRepository, ModuleRepository>();
            services.AddSingleton<IValidator<Module>, ModuleValidator>();

            services.AddSingleton<ILanguageManagementService, LanguageManagementService>();
            services.AddSingleton<ILanguageRepository, LanguageRepository>();
            services.AddSingleton<IValidator<Language>, LanguageValidator>();

            services.AddSingleton<IKeyManagementService, KeyManagementService>();
            services.AddSingleton<IKeyRepository, KeyRepository>();
            services.AddSingleton<IValidator<Key>, KeyValidator>();

            services.AddSingleton<IAssistantService, AssistantService>();

        }
    }
}
