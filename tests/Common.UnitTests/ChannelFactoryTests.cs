using System;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Common.UnitTests;

public sealed class ChannelFactoryTests
{
    [Fact]
    public void ChannelFactoryUsesDisposableObjectPoolInternally()
    {
        // Arrange
        var rabbitSettings = new RabbitMqSettings
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        // Act
        var channelFactory = new ChannelFactory(new FakeOption<RabbitMqSettings>(rabbitSettings));
        var internalPoolFieldInfo = channelFactory
            .GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(fieldInfo => fieldInfo.Name == "_channelPool");
        var internalPool = internalPoolFieldInfo.GetValue(channelFactory);
        
        // Assert
        (internalPool! is IDisposable).Should().BeTrue();
    }
}

public class FakeOption<T> : IOptions<T> where T: class
{
    public FakeOption(T value)
    {
        Value = value;
    }
    public T Value { get; }
}