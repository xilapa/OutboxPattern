using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Common.Messaging;

public sealed class ChannelFactory : IChannelFactory
{
    private readonly IConnection _connection;
    private readonly ObjectPool<IModel> _channelPool;

    public ChannelFactory(IConnection connection)
    {
        _connection = connection;
        // Using object pool provider to get an instance of DisposableObjectPool that is internal and sealed
        var objectPoolProvider = new DefaultObjectPoolProvider();
        _channelPool = objectPoolProvider.Create(new ChannelPooledObjectPolicy(_connection));
    }

    /// <summary>
    /// Gets a channel instance from the internal pool.
    /// </summary>
    /// <returns>A channel</returns>
    public IModel GetChannel()
    {
        return _channelPool.Get();
    }

    /// <summary>
    /// Returns a channel to the internal pool.
    /// </summary>
    /// <param name="channel">Channel</param>
    public void ReturnChannel(IModel channel)
    {
        _channelPool.Return(channel);
    }

    /// <summary>
    /// Uses a channel from the pool to execute an action.
    /// </summary>
    /// <param name="channelAction">Action using the channel</param>
    public void WithChannel(Action<IModel> channelAction)
    {
        var channel = _channelPool.Get();
        try
        {
            channelAction(channel);
        }
        finally
        {
            _channelPool.Return(channel);
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

public interface IChannelFactory
{
    IModel GetChannel();
    void ReturnChannel(IModel channel);
    void WithChannel(Action<IModel> channelAction);
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