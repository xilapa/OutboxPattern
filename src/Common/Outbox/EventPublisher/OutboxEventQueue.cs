using System.Threading.Channels;
using Common.Outbox.Base;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.EventPublisher;

public sealed class OutboxEventQueue : BaseQueue, IOutboxEventQueueWriter, IOutboxEventQueueReader
{
    public OutboxEventQueue(ILogger<OutboxEventQueue> logger)
    // If the channel is full, drop the message. It'll be re-queued later by the EventReviver
        : base(10_000, logger, BoundedChannelFullMode.DropWrite)
    { }
}

public interface IOutboxEventQueueWriter : IBaseQueueWriter
{ }

public interface IOutboxEventQueueReader : IBaseQueueReader
{ }


