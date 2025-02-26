
//var builder = Host.CreateApplicationBuilder(args);

//var host = builder.Build();
//host.Run();


using Blocks.Genesis;
using Worker;

const string _serviceName = "blocks-uilm-worker";

await ApplicationConfigurations.ConfigureLogAndSecretsAsync(_serviceName);

await CreateHostBuilder(args).Build().RunAsync();

IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, builder) =>
        {
            ApplicationConfigurations.ConfigureWorkerEnv(builder, args);
        })
        .ConfigureServices(async (services) =>
        {
            ApplicationConfigurations.ConfigureServices(services, DomainService.Utilities.Constants.GetMessageConfiguration());
            services.AddHttpClient();

            services.RegisterApplicationServices();
            ApplicationConfigurations.ConfigureWorker(services);
        });