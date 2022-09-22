using Common.Outbox.Base;

namespace Common.Outbox.EventPublisher;

public sealed class OutboxEventQueue : BaseQueue, IOutboxEventQueueWriter, IOutboxEventQueueReader
{
    // todo: make capacity configurable
    public OutboxEventQueue() : base(1200)
    { }
}

public interface IOutboxEventQueueWriter : IBaseQueueWriter
{ }

public interface IOutboxEventQueueReader : IBaseQueueReader
{ }


