using System.Text.Json;
using Common.Messaging;
using Common.Outbox.Base;
using Common.Outbox.EventSaver;
using Common.Outbox.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Outbox.EventRetry;

public sealed class OutboxEventRetryPublisher : BaseEventPublisher
{
    private readonly ILogger<OutboxEventRetryPublisher> _logger;
    private IModel? _channel;
    private readonly SenderSettings _senderSettings;
    private readonly IBasicProperties _defaultMessageProperties;
    private CancellationToken _ctx;

    public OutboxEventRetryPublisher(
        IOutboxEventRetryQueue retryQueue,
        IChannelFactory channelFactory,
        ILogger<OutboxEventRetryPublisher> logger,
        IOptions<SenderSettings> senderSettings,
        IOutboxEventSaveQueue saveQueue) :
        base (channelFactory, retryQueue, saveQueue, logger)
    {
        _logger = logger;
        _channel = GetConfiguredChannel();
        _senderSettings = senderSettings.Value;
        _defaultMessageProperties = channelFactory.WithChannel<IBasicProperties>(channel => channel.CreateBasicProperties());
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ctx = stoppingToken;
        SetCancelationTokenOnBaseClass(stoppingToken);
        return PublishEventsFromQueue(stoppingToken);
    }

    private async Task PublishEventsFromQueue(CancellationToken cancellationToken)
    {
        await foreach(var @event in RetryQueue.GetAllAsync(cancellationToken))
        {
            // todo: short circuit when channel is null
            try
            {
                // Increment the current retry count, to avoid infinity looping messages
                @event.IncrementRetryCount();
                await DelayPublishing(@event);
                var body = JsonSerializer.SerializeToUtf8Bytes(@event.EventData);
                var publishingKey = new PublishingKey(_channel!.ChannelNumber, _channel.NextPublishSeqNo);
                await EventsPendingConfirmation.AddWithRetries(publishingKey, @event, _logger);

                _channel.BasicPublish(_senderSettings.Exchange, @event.EventKey, true, _defaultMessageProperties, body);
            }
            catch (Exception e)
            {
                _logger.LogError(e,"Message: {MessageId} was not published on retry {Retry}, current retry {Current}",
                    @event.Id, @event.Retries, @event.CurrentRetries);
                await RecoverEvents(@event);
            }
        }
    }

    private static async Task DelayPublishing(OutboxEvent outboxEvent)
    {
        var mod = outboxEvent.CurrentRetries % 2;
        if (mod == 0)
            await Task.Delay(100);
        else
            await Task.Delay(50);
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

            // If it reached the maximum retries, just save it
            if (currentEvent.CurrentRetries >= 2)
            {
                await SaveQueue.Enqueue(currentEvent, _ctx);
                return;
            }

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
        if (@event!.CurrentRetries >= 2)
        {
            SaveQueue.Enqueue(@event, _ctx).AsTask().Wait(_ctx);
            return;
        }
        RetryQueue.Enqueue(@event, _ctx).AsTask().Wait(_ctx);
    }
}