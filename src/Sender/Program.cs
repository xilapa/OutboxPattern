using Common;
using Common.Messaging;
using Common.Outbox.EventPublisher;
using Common.Outbox.EventReviver;
using Common.Outbox.EventSaver;
using Common.Persistence;
using Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sender;

var host = new HostBuilder()
    .ConfigureDefaults(args)
    .ConfigureHostConfiguration(configurationBuilder =>
        configurationBuilder
            .SetBasePath(AppContext.BaseDirectory)
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddCommandLine(args)
    )
    .ConfigureAppConfiguration((hostContext, configurationBuilder) =>
    {
        var env = hostContext.HostingEnvironment;
        configurationBuilder
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddCommandLine(args);
    })
    .ConfigureServices((hostContext, serviceCollection) =>
        serviceCollection
            .AddRabbitMq(hostContext.Configuration)
            .Configure<SenderSettings>(hostContext.Configuration.GetSection(nameof(SenderSettings)))
            .AddDbContext<Context>()
            .AddSingleton<IOutboxEventQueue, OutboxEventQueue>()
            .AddHostedService<OutboxEventPublisher>()
            .AddSingleton<IOutboxEventSaveQueue, OutboxEventSaveQueue>()
            .AddHostedService<OutboxEventSaverService>()
            .AddHostedService<OutboxEventReviver>()
            .AddHostedService<WorkGenerator>()
    )
    .Build();

var senderSettings = host.Services.GetRequiredService<IOptions<SenderSettings>>().Value;

host.Services.DeclareQueue(typeof(SomeEntityCreated));

await host.RunAsync();