using System.Threading.Channels;

namespace Common.Outbox;

public abstract class BaseQueue : IQueueWriter, IQueueReader
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

    public async Task Enqueue(OutboxEvent @event, CancellationToken cancellationToken) =>
        await _queue.Writer.WriteAsync(@event, cancellationToken);

    public async Task<OutboxEvent> Dequeue(CancellationToken cancellationToken) =>
        await _queue.Reader.ReadAsync(cancellationToken);
}

public interface IQueueWriter
{
    Task Enqueue(OutboxEvent @event, CancellationToken cancellationToken);
}

public interface IQueueReader
{
    Task<OutboxEvent> Dequeue(CancellationToken cancellationToken);
}