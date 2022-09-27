using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.Base;

public abstract class BaseQueue : IBaseQueue
{
    private readonly int _capacity;
    private readonly ILogger _logger;
    private readonly Channel<OutboxEvent> _queue;

    protected BaseQueue(int capacity, ILogger logger, BoundedChannelFullMode channelMode = BoundedChannelFullMode.Wait)
    {
        _capacity = capacity;
        _logger = logger;
        var boundedChannelOptions = new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = channelMode
        };
        _queue = Channel.CreateBounded<OutboxEvent>(boundedChannelOptions);
    }

    public async ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken)
    {
        if(_queue.Reader.Count > _capacity * 0.9)
            _logger.LogWarning("{CurrentTime} Queue is under heavy load, current count: {Count}",DateTime.UtcNow, _queue.Reader.Count);
        await _queue.Writer.WriteAsync(@event, cancellationToken);
    }

    public async ValueTask<bool> TryEnqueue(OutboxEvent @event, CancellationToken cancellationToken)
    {
        var currentCount = _queue.Reader.Count;
        if (currentCount > _capacity - 1_000)
        {
            _logger.LogWarning("{CurrentTime} Event not enqueued, queue is under heavy load, current count: {Count}",DateTime.UtcNow, _queue.Reader.Count);
            return false;
        }
        await _queue.Writer.WriteAsync(@event, cancellationToken);
        return true;
    }

    public async ValueTask<OutboxEvent> Dequeue(CancellationToken cancellationToken) =>
        await _queue.Reader.ReadAsync(cancellationToken);

    public IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}

public interface IBaseQueueWriter
{
    ValueTask Enqueue(OutboxEvent @event, CancellationToken cancellationToken);

    /// <summary>
    /// If the remaining slots on the queue is lower than 1_000 elements, do not enqueue and return false.
    /// Otherwise, returns true.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<bool> TryEnqueue(OutboxEvent @event, CancellationToken cancellationToken);
}

public interface IBaseQueueReader
{
    ValueTask<OutboxEvent> Dequeue(CancellationToken cancellationToken);
    IAsyncEnumerable<OutboxEvent> GetAllAsync(CancellationToken cancellationToken);
}

public interface IBaseQueue: IBaseQueueWriter, IBaseQueueReader
{ }