using Domain.Entities;
using Domain.Events;
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
        var @event = someEntity.DomainEvents.First(_ => _ is SomeEntityCreated);

        (@event is SomeEntityCreated).Should().BeTrue();

        if (@event is not SomeEntityCreated someEntityCreated)
        {
            Assert.Fail("SomeEntityCreated was not created");
            return;
        }
        
        someEntityCreated.CreatedEntityId.Should().Be(someEntity.Id);
        someEntityCreated.CreatedEntityName.Should().Be(someEntity.Name);
    }
}