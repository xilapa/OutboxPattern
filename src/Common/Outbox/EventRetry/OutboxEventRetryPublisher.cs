using System.Text.Json;
using Common.Messaging;
using Common.Outbox.Base;
using Common.Outbox.EventSaver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Common.Outbox.EventRetry;

public sealed class OutboxEventRetryPublisher : BaseEventPublisher
{
    private readonly SenderSettings _senderSettings;

    public OutboxEventRetryPublisher(
        IChannelFactory channelFactory,
        IOutboxEventRetryQueue retryQueue,
        IOutboxEventSaveQueue saveQueue,
        ILogger<OutboxEventRetryPublisher> logger,
        IOptions<SenderSettings> senderSettings) :
        base (channelFactory, retryQueue, saveQueue, retryQueue,  logger)
    {
        _senderSettings = senderSettings.Value;
    }

    protected override async Task PublishEvent(OutboxEvent @event)
    {
        // If it reached the maximum retries, just save it
        if (@event.CurrentRetries >= 2)
        {
            await SaveQueue.Enqueue(@event, CancellationToken);
            return;
        }

        await DelayPublishing(@event);
        var body = JsonSerializer.SerializeToUtf8Bytes(@event.EventData);
        Channel.BasicPublish(_senderSettings.Exchange, @event.EventKey, DefaultMessageProperties, body);

        // Increment the current retry count, to avoid infinity looping messages
        @event.IncrementRetryCount();
    }

    private static async Task DelayPublishing(OutboxEvent outboxEvent)
    {
        // if the message just arrived, doesn't delay anything
        if (outboxEvent.CurrentRetries == 0)
            return;

        var mod = outboxEvent.CurrentRetries % 2;
        if (mod == 0)
            await Task.Delay(100);
        else
            await Task.Delay(50);
    }

    protected override void RedirectNackMessage(OutboxEvent @event)
    {
        if (@event.CurrentRetries >= 2)
        {
            SaveQueue.Enqueue(@event, CancellationToken).AsTask().Wait(CancellationToken);
            return;
        }
        RetryQueue.Enqueue(@event, CancellationToken).AsTask().Wait(CancellationToken);
    }
}