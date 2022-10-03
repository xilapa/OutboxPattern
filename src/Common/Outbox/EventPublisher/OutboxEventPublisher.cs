using System.Collections.Concurrent;
using System.Text.Json;
using Common.Messaging;
using Common.Outbox.EventSaver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Outbox.EventPublisher;

public sealed class OutboxEventPublisher : BackgroundService
{
    private record PublishingKey(int ChannelId, ulong MessageId);

    private readonly IOutboxEventSaveQueue _saveQueue;
    private readonly IBasicProperties _defaultMessageProperties;
    private readonly IModel _channel;
    private readonly ConcurrentDictionary<PublishingKey, OutboxEvent> _eventsPendingConfirmation;
    private readonly IOutboxEventQueue _eventsQueue;
    private readonly ILogger<OutboxEventPublisher> _logger;
    private bool _channelWasClosed;

    public OutboxEventPublisher(
        IChannelFactory channelFactory,
        IOutboxEventQueue eventsQueue,
        IOutboxEventSaveQueue saveQueue,
        ILogger<OutboxEventPublisher> logger)
    {
        _eventsPendingConfirmation = new ConcurrentDictionary<PublishingKey, OutboxEvent>();
        _saveQueue = saveQueue;
        _eventsQueue = eventsQueue;
        _logger = logger;
        _channel = channelFactory.GetChannel();
        _channel.ConfirmSelect();
        _channel.BasicAcks += HandleAcks;
        _channel.BasicNacks += HandleNacks;
        _defaultMessageProperties = _channel.CreateBasicProperties();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{CurrentTime}: Started Listening for messages to publish", DateTime.UtcNow);
        return PublishEventsFromQueue(stoppingToken);
    }

    private async Task PublishEventsFromQueue(CancellationToken cancellationToken)
    {
        await foreach(var @event in _eventsQueue.GetAllAsync(cancellationToken))
        {
            // If the channel is closed save the status on the database
            // The client is auto-recovering
            if (_channel.IsClosed)
            {
                await _saveQueue.Enqueue(@event, cancellationToken);
                _logger.LogError("{CurrentTime}: Channel closed, current message Id: {MessageId}",
                    DateTime.UtcNow, @event.Id);
                await Task.Delay(300, cancellationToken);
                _channelWasClosed = true;
            }

            // Log when the connection is reestablished after a failure
            if (_channel.IsOpen && _channelWasClosed)
            {
                _logger.LogInformation("{CurrentTime}: Connection with channel reestablished", DateTime.UtcNow);
                _channelWasClosed = false;
            }

            try
            {
                var publishingKey = new PublishingKey(_channel.ChannelNumber, _channel.NextPublishSeqNo);
                _eventsPendingConfirmation.TryAdd(publishingKey, @event);
                var body = JsonSerializer.SerializeToUtf8Bytes(@event.EventData);
                _channel!.BasicPublish("", @event.EventKey, _defaultMessageProperties, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e,"{CurrentTime}: Error on publishing, current message Id: {MessageId}",
                    DateTime.UtcNow, @event.Id);
                await _saveQueue.Enqueue(@event, cancellationToken);
            }
        }
    }

    private void HandleAcks(object? channel, BasicAckEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var key = new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag);
        var eventFound = _eventsPendingConfirmation.TryRemove(key, out var @event);
        if (!eventFound) return;
        @event!.SetPublishedStatus();
        _saveQueue.Enqueue(@event, CancellationToken.None).AsTask().Wait();
    }

    private void HandleNacks(object? channel, BasicNackEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var key = new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag);
        var eventFound = _eventsPendingConfirmation.TryRemove(key, out var @event);
        if (!eventFound || @event is null) return;
        _saveQueue.Enqueue(@event, CancellationToken.None).AsTask().Wait();
    }
}