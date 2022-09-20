using Common;
using Common.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureDefaults(args)
    .ConfigureHostConfiguration(configurationBuilder =>
        configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddCommandLine(args)
    )
    .ConfigureAppConfiguration((hostContext, configurationBuilder) =>
    {
        var env = hostContext.HostingEnvironment;
        configurationBuilder
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddCommandLine(args);
    })
    .ConfigureServices((hostContext, serviceCollection) =>
        serviceCollection
            .AddRabbitMq(hostContext.Configuration)
            .Configure<SenderSettings>(hostContext.Configuration.GetSection(nameof(SenderSettings)))
    )
    .Build();

host.Services.DeclareExchange(new ExchangeDefinition
{
    Name = "outbox_exchange",
    Type = ExchangeType.Direct,
    Durable = false,
    AutoDelete = true
});

await host.RunAsync();
    
