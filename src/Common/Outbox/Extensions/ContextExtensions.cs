using Common.Persistence;
using Domain.Entities;

namespace Common.Outbox.Extensions;

public static class ContextExtensions
{
    public static IEnumerable<OutboxEvent> GetOutboxEventsAndAddToTransaction(this Context context)
    {
        var outboxEvents = context
            .ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(domainEvent => new OutboxEvent(domainEvent))
            .ToArray();

        foreach (var outboxEvent in outboxEvents)
            context.Add(outboxEvent);

        return outboxEvents;
    }
}