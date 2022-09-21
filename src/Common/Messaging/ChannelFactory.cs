using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Common.Messaging;

public sealed class ChannelFactory : IChannelFactory
{
    private readonly ConnectionFactory _connectionFactory;
    private IConnection _connection;
    private ObjectPool<IModel> _channelPool;

    public ChannelFactory(RabbitMqSettings rabbitMqSettings)
    {
        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqSettings.HostName,
            UserName = rabbitMqSettings.UserName,
            Password = rabbitMqSettings.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
        var connection = _connectionFactory.CreateConnection();
        _connection = connection;
        _channelPool = CreateChannelPool(_connection);
    }

    private static ObjectPool<IModel> CreateChannelPool(IConnection connection)
    {
        // Using object pool provider to get an instance of DisposableObjectPool that is internal and sealed
        var objectPoolProvider = new DefaultObjectPoolProvider {MaximumRetained = Environment.ProcessorCount * 10};
         return objectPoolProvider.Create(new ChannelPooledObjectPolicy(connection));
    }

    /// <summary>
    /// Gets a channel instance from the internal pool.
    /// </summary>
    /// <returns>A channel</returns>
    public IModel GetChannel()
    {
        CheckAndRecoverConnection();
        var channel = _channelPool.Get();
        if (channel.IsOpen) return channel;
        channel.Dispose();
        return GetChannel();
    }

    /// <summary>
    /// Returns a channel to the internal pool.
    /// </summary>
    /// <param name="channel">Channel</param>
    public void ReturnChannel(IModel channel)
    {
        CheckAndRecoverConnection();

        if (channel.IsClosed)
            channel.Dispose();
        else
            _channelPool.Return(channel);
    }

    private void CheckAndRecoverConnection()
    {
        if (_connection.IsOpen) return;
        _connection.Dispose();
        _connection = _connectionFactory.CreateConnection();
        _channelPool = CreateChannelPool(_connection);
    }

    /// <summary>
    /// Uses a channel from the pool to execute an action.
    /// </summary>
    /// <param name="channelAction">Action using the channel</param>
    public void WithChannel(Action<IModel> channelAction)
    {
        var channel = GetChannel();
        try
        {
            channelAction(channel);
        }
        finally
        {
            ReturnChannel(channel);
        }
    }

    /// <summary>
    /// Uses a channel from the pool to execute a func.
    /// </summary>
    /// <param name="channelFunc">Func using the channel</param>
    public T WithChannel<T>(Func<IModel, T> channelFunc)
    {
        var channel = GetChannel();
        try
        {
            var result = channelFunc(channel);
            return result;
        }
        finally
        {
            ReturnChannel(channel);
        }
    }

    /// <summary>
    /// Uses a channel from the pool to execute an asynchronous func.
    /// </summary>
    /// <param name="channelFunc">Func using the channel</param>
    public async Task WithChannel(Func<IModel, Task> channelFunc)
    {
        var channel = GetChannel();
        try
        {
            await channelFunc(channel);
        }
        finally
        {
            ReturnChannel(channel);
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

public interface IChannelFactory : IDisposable
{
    IModel GetChannel();
    void ReturnChannel(IModel channel);
    void WithChannel(Action<IModel> channelAction);
    T WithChannel<T>(Func<IModel, T> channelFunc);
    Task WithChannel(Func<IModel, Task> channelFunc);
}

internal sealed class ChannelPooledObjectPolicy : PooledObjectPolicy<IModel>
{
    private readonly IConnection _connection;

    public ChannelPooledObjectPolicy(IConnection connection)
    {
        _connection = connection;
    }

    public override IModel Create()
    {
        return _connection.CreateModel();
    }

    public override bool Return(IModel obj)
    {
        return true;
    }
}