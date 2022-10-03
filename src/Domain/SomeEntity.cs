namespace Domain;

public abstract class BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = new ();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents;
    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class SomeEntity : BaseEntity
{
    public SomeEntity(string name)
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
        Name = name;
        AddDomainEvent(new SomeEntityCreated(this));
    }

    public Guid Id { get; private set; }
    public DateTime CreationDate { get; private set; }
    public string Name { get; private set; }
}