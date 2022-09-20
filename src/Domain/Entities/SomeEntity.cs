using Domain.Events;

namespace Domain.Entities;

public sealed class SomeEntity : BaseEntity
{
    public SomeEntity(string name)
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
        Name = name;
        AddDomainEvent(new SomeEntityCreated(this));
    }

    public Guid Id { get; protected set; }
    public DateTime CreationDate { get; protected set; }
    public string Name { get; protected set; }
}