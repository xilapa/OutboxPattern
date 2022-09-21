using Domain.Events;

namespace Domain.Entities;

public abstract class BaseEntity
{
    public BaseEntity()
    {
        _domainEvents = new List<DomainEvent>();
    }

    private readonly List<DomainEvent> _domainEvents;
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents;
    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

}