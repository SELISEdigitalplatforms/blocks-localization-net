using Api;
using Blocks.Genesis;

const string _serviceName = "blocks-localization-api";
var blocksSecret = await ApplicationConfigurations.ConfigureLogAndSecretsAsync(_serviceName);
var builder = WebApplication.CreateBuilder(args);

ApplicationConfigurations.ConfigureAppConfigs(builder, args);
var services = builder.Services;

builder.Services.RegisterApplicationServices();

ApplicationConfigurations.ConfigureServices(services, new MessageConfiguration
{
     Queues = new List<string> { },
     Topics = new List<string> { }
});

ApplicationConfigurations.ConfigureApi(services);

var app = builder.Build();

ApplicationConfigurations.ConfigureMiddleware(app);

await app.RunAsync();
