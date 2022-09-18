using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Common.Messaging;

public static class SetupDependencyInjection
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqSettings = configuration.GetRequiredSection("RabbitMq").Get<RabbitMqSettings>();
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqSettings.HostName,
            UserName = rabbitMqSettings.UserName,
            Password = rabbitMqSettings.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
        var connection = connectionFactory.CreateConnection();
        services.AddSingleton<IChannelFactory>(new ChannelFactory(connection));
        return services;
    }

    public static IServiceProvider DeclareExchange(this IServiceProvider serviceProvider,
        ExchangeDefinition exchangeDef)
    {
        var channelFactory = serviceProvider.GetRequiredService<IChannelFactory>();
        using var channel = channelFactory.CreateChannel();
        channel.ExchangeDeclare(
            exchangeDef.Name,
            exchangeDef.GetExchangeType(),
            exchangeDef.Durable,
            exchangeDef.AutoDelete);
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