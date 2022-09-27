using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Common.Messaging;

public static class SetupDependencyInjection
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<IChannelFactory,ChannelFactory>();
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

    public static IServiceProvider DeclareQueueAndBind(this IServiceProvider serviceProvider, Type messageType,
        QueueDefinition queueDefinition)
    {
        var channelFactory = serviceProvider.GetRequiredService<IChannelFactory>();
        channelFactory.WithChannel(channel =>
        {
            channel.QueueDeclare(messageType.Name, durable: queueDefinition.Durable, exclusive: queueDefinition.Exclusive,
                autoDelete: queueDefinition.AutoDelete);
            channel.QueueBind(messageType.Name, queueDefinition.ExchangeToBind,messageType.Name);
        });
        return serviceProvider;
    }
}

public sealed class ExchangeDefinition
{
    public string Name { get; set; }
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

public sealed class QueueDefinition
{
    public string ExchangeToBind { get; set; }
    public bool Durable { get; set; }
    public bool AutoDelete { get; set; }
    public bool Exclusive { get; set; }
}