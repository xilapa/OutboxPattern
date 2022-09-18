using RabbitMQ.Client;

namespace Common.Messaging;

public sealed class ChannelFactory : IChannelFactory
{
    private readonly IConnection _connection;

    public ChannelFactory(IConnection connection)
    {
        _connection = connection;
    }

    public IModel CreateChannel()
    {
        // TODO: Use object pool to reuse channels
        return _connection.CreateModel();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

public interface IChannelFactory : IDisposable
{
    IModel CreateChannel();
}