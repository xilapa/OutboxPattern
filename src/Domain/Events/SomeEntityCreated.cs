using Domain.Entities;

namespace Domain.Events;

public sealed class SomeEntityCreated : BaseEvent
{
    public SomeEntityCreated(SomeEntity someEntity)
    {
        CreatedEntityId = someEntity.Id;
        CreatedEntityName = someEntity.Name;
    }

    public Guid CreatedEntityId { get; protected set; }
    public string CreatedEntityName { get; protected set; }
}