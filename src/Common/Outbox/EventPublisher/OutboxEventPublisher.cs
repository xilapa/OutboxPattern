using System.Text.Json;
using Common.Messaging;
using Common.Outbox.Base;
using Common.Outbox.EventRetry;
using Common.Outbox.EventSaver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Outbox.EventPublisher;

/// <summary>
/// Try to publish the events to the broker
/// </summary>
public sealed class OutboxEventPublisher : BaseEventPublisher
{
    private readonly SenderSettings _senderSettings;

    public OutboxEventPublisher(
        IChannelFactory channelFactory,
        IOutboxEventQueueReader outboxEventsQueue,
        IOutboxEventSaveQueue saveQueue,
        IOutboxEventRetryQueue retryQueue,
        ILogger<OutboxEventPublisher> logger,
        IOptions<SenderSettings> senderSettings) :
        base (channelFactory, outboxEventsQueue, saveQueue, retryQueue, logger)
    {
        _senderSettings = senderSettings.Value;
    }

    protected override Task<bool> PublishEvent(OutboxEvent @event)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(@event.EventData);
        Channel!.BasicPublish(_senderSettings.Exchange, @event.EventKey, true, DefaultMessageProperties, body);
        return Task.FromResult(true);
    }

    protected override void RedirectNackMessage(OutboxEvent @event)
    {
        RetryQueue.Enqueue(@event, CancellationToken).AsTask().Wait(CancellationToken);
    }
}