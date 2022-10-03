using System.Threading.Channels;

namespace Common.Outbox;

public abstract class BaseQueue : IBaseQueue
{
    private readonly Channel<OutboxEvent> _queue;

    protected BaseQueue(int capacity, BoundedChannelFullMode channelMode = BoundedChannelFullMode.Wait)
    {
        var boundedChannelOptions = new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = channelMode
        };
        _queue = Channel.CreateBounded<OutboxEvent>(boundedChannelOptions);
    }

    public async ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken) =>
        await _queue.Writer.WriteAsync(@event, cancellationToken);

    public IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}

public interface IBaseQueue
{
    ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken);
    IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken);
}