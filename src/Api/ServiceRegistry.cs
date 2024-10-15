using DomainService.Repositories;
using DomainService.Services;
using FluentValidation;

namespace Api
{
    public static class ServiceRegistry
    {
        public static void RegisterApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<IModuleManagementService, ModuleManagementService>();
            services.AddSingleton<IModuleRepository, ModuleRepository>();
            services.AddSingleton<IValidator<Module>, ModuleValidator>();
        }
    }
}
