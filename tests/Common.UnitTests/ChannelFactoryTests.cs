using System;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using FluentAssertions;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace Common.UnitTests;

public sealed class ChannelFactoryTests
{
    [Fact]
    public void ChannelFactoryUsesDisposableObjectPoolInternally()
    {
        // Arrange
        var mockConnection = new Mock<IConnection>();
        
        // Act
        var channelFactory = new ChannelFactory(mockConnection.Object);
        var internalPoolFieldInfo = channelFactory
            .GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(fieldInfo => fieldInfo.Name == "_channelPool");
        var internalPool = internalPoolFieldInfo.GetValue(channelFactory);
        
        // Assert
        (internalPool! is IDisposable).Should().BeTrue();
    }
}