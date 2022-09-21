namespace Common.Outbox.EventPublisher;

public sealed class EventQueue : BaseQueue
{
    public EventQueue() : base(Environment.ProcessorCount * 3)
    { }
}
