using System.Text.Json;

namespace Domain.Outbox;

public sealed class OutboxEvent
{
    // for EF Core
    private OutboxEvent()
    {
        
    }

    public OutboxEvent(object @event)
    {
        Id = Guid.NewGuid();
        EventDate = DateTime.UtcNow;
        EventKey = @event.GetType().Name;
        EventData = JsonSerializer.Serialize(@event);
    }

    public Guid Id { get; protected set; }
    public string EventKey { get; protected set; }
    public string EventData { get; protected set; }
    public DateTime EventDate { get; protected set; }
}