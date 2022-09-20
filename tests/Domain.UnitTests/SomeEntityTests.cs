using Common;
using Common.Persistence;
using Domain.Entities;
using Domain.Events;
using Domain.Outbox;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests;

public sealed class SomeEntityTests
{
    [Fact]
    public void SomeEntityCreatedEventIsCreatedCorrectly()
    {
        // Arrange and Act
        var someEntity = new SomeEntity("test");
        
        // Assert
        var @event = someEntity.BaseEvents.First(_ => _ is SomeEntityCreated);

        (@event is SomeEntityCreated).Should().BeTrue();
        
        if(@event is SomeEntityCreated someEntityCreated)
        {
            someEntityCreated.CreatedEntityId.Should().Be(someEntity.Id);
            someEntityCreated.CreatedEntityName.Should().Be(someEntity.Name);
        }
    }
}