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

    public static IServiceProvider DeclareQueue(this IServiceProvider serviceProvider, Type messageType)
    {
        var channelFactory = serviceProvider.GetRequiredService<IChannelFactory>();
        channelFactory.WithChannel(channel =>
                channel.QueueDeclare(messageType.Name, durable: true, exclusive: false, autoDelete: false));
        return serviceProvider;
    }
}