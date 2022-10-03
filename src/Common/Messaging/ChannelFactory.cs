using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Common.Messaging;

public sealed class ChannelFactory : IChannelFactory
{
    private readonly IConnection? _connection;

    public ChannelFactory(IOptions<RabbitMqSettings> rabbitMqSettingsOpt)
    {
        var rabbitMqSettings = rabbitMqSettingsOpt.Value;
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqSettings.HostName,
            UserName = rabbitMqSettings.UserName,
            Password = rabbitMqSettings.Password,
            AutomaticRecoveryEnabled = true
        };
        var connection = connectionFactory.CreateConnection();
        _connection = connection;
    }

    public IModel GetChannel()
    {
        var channel = _connection!.CreateModel();
        if (channel.IsOpen) return channel;
        channel.Dispose();
        return GetChannel();
    }

    public void WithChannel(Action<IModel> channelAction)
    {
        var channel = GetChannel();
        try
        {
            channelAction(channel);
        }
        finally
        {
            channel.Dispose();
        }
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}

public interface IChannelFactory : IDisposable
{
    IModel GetChannel();
    void WithChannel(Action<IModel> channelAction);
}