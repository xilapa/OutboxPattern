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

    public Guid Id { get; private set; }
    public DateTime CreationDate { get; private set; }
    public string Name { get; private set; }
}