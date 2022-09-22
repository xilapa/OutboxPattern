﻿using System.Collections.Concurrent;
using Common.Messaging;
using Common.Outbox.EventRetrier;
using Common.Outbox.EventSaver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Outbox.Base;

public abstract class BaseEventPublisher : BackgroundService
{
    protected readonly IOutboxEventRetryQueue RetryQueue;
    protected readonly IOutboxEventSaveQueue SaveQueue;
    protected readonly ConcurrentDictionary<PublishingKey, OutboxEvent> EventsPendingConfirmation;
    private readonly IChannelFactory _channelFactory;
    private readonly ILogger _logger;
    private CancellationToken _ctx;

    protected BaseEventPublisher(
        IChannelFactory channelFactory,
        IOutboxEventRetryQueue retryQueue,
        IOutboxEventSaveQueue saveQueue,
        ILogger logger)
    {
        _channelFactory = channelFactory;
        RetryQueue = retryQueue;
        SaveQueue = saveQueue;
        _logger = logger;
        EventsPendingConfirmation = new ConcurrentDictionary<PublishingKey, OutboxEvent>();
    }

    protected void SetCancelationTokenOnBaseClass(CancellationToken ctx)
    {
        _ctx = ctx;
    }

    protected IModel? GetConfiguredChannel()
    {
        try
        {
            var channel = _channelFactory.GetChannel();
            channel.ConfirmSelect();
            channel.BasicAcks += HandleAcks;
            channel.BasicNacks += HandleNacks;
            channel.ModelShutdown += HandleShutdown;
            return channel;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Cannot connect to broker");
            return null;
        }
    }

    protected async ValueTask RedirectMessages(IBaseQueueWriter destinationQueue, OutboxEvent? currentEvent = null)
    {
        // Take an snapshot of the keys
        var allKeys = EventsPendingConfirmation.Keys.ToArray();
        // Try remove each key, an event could not exists anymore in the dict due to Handle Ack, Nack or Shutdown
        foreach (var key in allKeys)
        {
            var removed = EventsPendingConfirmation.TryRemove(key, out var eventFromDeadChannel);
            // If the event doesn't exists continue
            if (!removed) continue;
            // If the removed event is the current, continue
            if (eventFromDeadChannel!.Id.Equals(currentEvent?.Id)) continue;
            await destinationQueue.Enqueue(eventFromDeadChannel, _ctx);
        }

        // Redirect the current event
        if (currentEvent is not null)
            await destinationQueue.Enqueue(currentEvent, _ctx);
    }

    private void HandleAcks(object? channel, BasicAckEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var eventFound = EventsPendingConfirmation.TryRemove(new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag), out var @event);
        if (!eventFound) return;
        @event!.SetPublishedStatus();
        SaveQueue.Enqueue(@event, CancellationToken.None).AsTask().Wait(_ctx);
    }

    protected abstract void HandleNacks(object? channel, BasicNackEventArgs eventArgs);

    private void HandleShutdown(object? channel, ShutdownEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        channelCasted.BasicAcks -= HandleAcks;
        channelCasted.BasicNacks -= HandleNacks;
        channelCasted.ModelShutdown -= HandleShutdown;
        RedirectMessages(RetryQueue).AsTask().Wait(_ctx);
    }
}