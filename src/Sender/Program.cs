using Common;
using Common.Messaging;
using Common.Outbox;
using Common.Outbox.EventPublisher;
using Common.Outbox.EventRetry;
using Common.Outbox.EventReviver;
using Common.Outbox.EventSaver;
using Common.Persistence;
using Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sender;

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
            .AddDbContext<Context>()
            .AddSingleton<OutboxEventQueue>()
            .AddSingleton<IOutboxEventQueueWriter>(sp => sp.GetRequiredService<OutboxEventQueue>())
            .AddSingleton<IOutboxEventQueueReader>(sp => sp.GetRequiredService<OutboxEventQueue>())
            .AddSingleton<IOutboxEventRetryQueue, OutboxEventRetryQueue>()
            .AddHostedService<OutboxEventRetryPublisher>()
            .AddHostedService<OutboxEventPublisher>()
            .AddSingleton<IOutboxEventSaveQueue, OutboxEventSaveQueue>()
            .AddHostedService<EventSaverService>()
            .AddSingleton<IDatabaseConnection, DatabaseConnection>()
            .AddHostedService<OutboxEventReviver>()
            .AddHostedService<WorkGenerator>()
    )
    .Build();

var senderSettings = host.Services.GetRequiredService<IOptions<SenderSettings>>().Value;

host.Services
    .DeclareExchange(new ExchangeDefinition
    {
        Name = senderSettings.Exchange,
        Type = ExchangeType.Direct,
        Durable = true,
        AutoDelete = false
    })
    .DeclareQueueAndBind(typeof(SomeEntityCreated),
        new QueueDefinition
        {
            Durable = false,
            AutoDelete = false,
            ExchangeToBind = senderSettings.Exchange
        });

await host.RunAsync();