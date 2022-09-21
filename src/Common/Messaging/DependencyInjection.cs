using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Common.Messaging;

public static class SetupDependencyInjection
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqSettings>(configuration.GetSection(nameof(SenderSettings)));
        // .Configure<>(hostContext.Configuration.GetSection(nameof(SenderSettings)))
        services.AddSingleton<IChannelFactory>();
        return services;
    }

    public static IServiceProvider DeclareExchange(this IServiceProvider serviceProvider,
        ExchangeDefinition exchangeDef)
    {
        var channelFactory = serviceProvider.GetRequiredService<IChannelFactory>();
        channelFactory.WithChannel(channel =>
        {
            channel.ExchangeDeclare(
                exchangeDef.Name,
                exchangeDef.GetExchangeType(),
                exchangeDef.Durable,
                exchangeDef.AutoDelete);
        });
        return serviceProvider;
    }
}

public sealed class ExchangeDefinition
{
    public string? Name { get; set; }
    public ExchangeType Type { get; set; }
    public bool Durable { get; set; }
    public bool AutoDelete { get; set; }

    public string GetExchangeType() =>
        Type switch
        {
            ExchangeType.Direct => "direct",
            ExchangeType.Fanout => "fanout",
            ExchangeType.Headers => "headers",
            ExchangeType.Topic => "topic",
            _ => throw new ArgumentOutOfRangeException()
        };
}

public enum ExchangeType
{
    Direct = 1,
    Fanout,
    Headers,
    Topic
}