using Domain.Entities;

namespace Domain.Events;

public sealed class SomeEntityCreated : DomainEvent
{
    public SomeEntityCreated(SomeEntity someEntity)
    {
        CreatedEntityId = someEntity.Id;
        CreatedEntityName = someEntity.Name;
    }

    public Guid CreatedEntityId { get; private set; }
    public string CreatedEntityName { get; private set; }
}