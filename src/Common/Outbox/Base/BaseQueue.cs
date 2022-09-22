using System.Threading.Channels;

namespace Common.Outbox.Base;

public abstract class BaseQueue : IBaseQueue
{
    private readonly Channel<OutboxEvent> _queue;

    protected BaseQueue(int capacity)
    {
        var boundedChannelOptions = new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<OutboxEvent>(boundedChannelOptions);
    }

    public async ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken) =>
        await _queue.Writer.WriteAsync(@event, cancellationToken);

    public async ValueTask<OutboxEvent> Dequeue(CancellationToken cancellationToken) =>
        await _queue.Reader.ReadAsync(cancellationToken);

    public IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}

public interface IBaseQueueWriter
{
    ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken);
}

public interface IBaseQueueReader
{
    ValueTask<OutboxEvent> Dequeue(CancellationToken cancellationToken);
    IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken);
}

public interface IBaseQueue: IBaseQueueWriter, IBaseQueueReader
{ }