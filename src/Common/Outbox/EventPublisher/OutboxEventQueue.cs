using System.Threading.Channels;

namespace Common.Outbox.EventPublisher;

public sealed class OutboxEventQueue : BaseQueue, IOutboxEventQueue
{
    public OutboxEventQueue()
    // If the channel is full, drop the message. It'll be re-queued later by the EventReviver
        : base(10_000, BoundedChannelFullMode.DropWrite)
    { }
}

public interface IOutboxEventQueue : IBaseQueue
{ }


