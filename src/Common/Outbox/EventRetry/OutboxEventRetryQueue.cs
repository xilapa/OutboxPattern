using Common.Outbox.Base;

namespace Common.Outbox.EventRetry;

public sealed class OutboxEventRetryQueue : BaseQueue, IOutboxEventRetryQueue
{
    // todo: make capacity configurable
    public OutboxEventRetryQueue() : base(2400)
    { }
}

public interface IOutboxEventRetryQueue : IBaseQueue
{ }