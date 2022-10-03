namespace Common.Outbox.EventSaver;

public sealed class OutboxEventSaveQueue : BaseQueue, IOutboxEventSaveQueue
{
    public OutboxEventSaveQueue() : base(20_000)
    { }
}

public interface IOutboxEventSaveQueue : IBaseQueue
{ }