using Domain.Events;

namespace Domain.Entities;

public abstract class BaseEntity
{
    public BaseEntity()
    {
        _domainEvents = new List<BaseEvent>();
    }

    private readonly List<BaseEvent> _domainEvents;
    public IReadOnlyCollection<BaseEvent> BaseEvents => _domainEvents;
    public void AddDomainEvent(BaseEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(BaseEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

}