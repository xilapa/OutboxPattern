using System.Text.Json;
using Domain.Events;

namespace Common.Outbox;

public sealed class OutboxEvent
{
    // for EF Core
    private OutboxEvent()
    { }

    public OutboxEvent(DomainEvent @event)
    {
        Id = Guid.NewGuid();
        EventDate = DateTime.UtcNow;
        EventKey = @event.GetType().Name;
        EventData = JsonSerializer.Serialize(@event as object);
    }

    public Guid Id { get; protected set; }
    public string EventKey { get; protected set; }
    public string EventData { get; protected set; }
    public DateTime EventDate { get; protected set; }
}