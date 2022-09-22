using System.Text.Json;
using Common.Messaging;
using Common.Outbox.Base;
using Common.Outbox.EventRetrier;
using Common.Outbox.EventSaver;
using Common.Outbox.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using IBasicProperties = RabbitMQ.Client.IBasicProperties;

namespace Common.Outbox.EventPublisher;

/// <summary>
/// Try to publish the events to the broker
/// </summary>
public sealed class OutboxEventPublisher : BaseEventPublisher
{
    private readonly ILogger<OutboxEventPublisher> _logger;
    private readonly IOutboxEventQueueReader _outboxBoxQueueReader;
    private readonly IBasicProperties _defaultMessageProperties;
    private readonly SenderSettings _senderSettings;
    private IModel? _channel;
    private CancellationToken _ctx;

    public OutboxEventPublisher(
        ILogger<OutboxEventPublisher> logger,
        IOutboxEventRetryQueue retryQueue,
        IOutboxEventQueueReader outboxBoxQueueReader,
        IChannelFactory channelFactory,
        IOptions<SenderSettings> senderSettings,
        IOutboxEventSaveQueue saveQueue) :
        base (channelFactory, retryQueue, saveQueue, logger)
    {
        _logger = logger;
        _outboxBoxQueueReader = outboxBoxQueueReader;
        _defaultMessageProperties = channelFactory.WithChannel<IBasicProperties>(channel => channel.CreateBasicProperties());
        _senderSettings = senderSettings.Value;
        _channel = GetConfiguredChannel();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ctx = stoppingToken;
        SetCancelationTokenOnBaseClass(stoppingToken);
        _logger.LogInformation("Started Listening for messages to publish {CurrentTime}", DateTime.Now);
        return PublishEventsFromQueue(stoppingToken);
    }

    private async Task PublishEventsFromQueue(CancellationToken cancellationToken)
    {
        await foreach(var @event in _outboxBoxQueueReader.GetAllAsync(cancellationToken))
        {
            // todo: short circuiting if a channel is null
            try
            {
                var body = JsonSerializer.SerializeToUtf8Bytes(@event.EventData);
                var publishingKey = new PublishingKey(_channel!.ChannelNumber, _channel.NextPublishSeqNo);
                await EventsPendingConfirmation.AddWithRetries(publishingKey, @event, _logger);

                _channel.BasicPublish(_senderSettings.Exchange, @event.EventKey, true, _defaultMessageProperties, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e,"Message: {MessageId} was not published on first try", @event.Id);
                await RecoverEvents(@event);
            }
        }
    }

    private async Task RecoverEvents(OutboxEvent currentEvent)
    {
        // If the channel is open, it could be a problem with the concurrent dictionary
        // used to track the published events
        if (_channel?.IsOpen is true)
        {
            // Try remove the current event from the published dictionary
            var allKeys = EventsPendingConfirmation.Keys.ToArray();
            foreach (var key in allKeys)
            {
                var found = EventsPendingConfirmation.TryGetValue(key, out var possibleEvent);
                if (!found) continue;
                if (!possibleEvent!.Id.Equals(currentEvent.Id)) continue;
                EventsPendingConfirmation.TryRemove(key, out _);
                break;
            }

            // Send it to the retry queue
            await RetryQueue.Enqueue(currentEvent, _ctx);
            return;
        }

        // Try get a new channel
        _channel = GetConfiguredChannel();

        // If can't get a new channel, the broker is unreachable
        // So, just save the messages on database to retry later
        if (_channel is null)
        {
            // If the channel is null, the key was not created and the event is lost in memory
            await RedirectMessages(SaveQueue, currentEvent);
            return;
        }

        // If can get a new channel, redirect the messages to retry queue
        await RedirectMessages(RetryQueue, currentEvent);
    }

    protected override void HandleNacks(object? channel, BasicNackEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var eventFound = EventsPendingConfirmation.TryRemove(new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag), out var @event);
        if (!eventFound) return;
        RetryQueue.Enqueue(@event!, _ctx).AsTask().Wait(_ctx);
    }
}