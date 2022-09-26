using System.Collections.Concurrent;
using Common.Messaging;
using Common.Outbox.EventRetry;
using Common.Outbox.EventSaver;
using Common.Outbox.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Outbox.Base;

public abstract class BaseEventPublisher : BackgroundService
{
    protected readonly IOutboxEventSaveQueue SaveQueue;
    protected readonly IOutboxEventRetryQueue RetryQueue;
    protected IBasicProperties DefaultMessageProperties;
    protected IModel? Channel;
    protected CancellationToken CancellationToken;

    private readonly ConcurrentDictionary<PublishingKey, OutboxEvent> _eventsPendingConfirmation;
    private readonly IChannelFactory _channelFactory;
    private readonly IBaseQueueReader _listenQueue;
    private readonly ILogger _logger;

    protected BaseEventPublisher(
        IChannelFactory channelFactory,
        IBaseQueueReader listenQueue, // queue to listen for events
        IOutboxEventSaveQueue saveQueue, // queue to send events to save in case of acks and failures
        IOutboxEventRetryQueue retryQueue, // queue to send events to retry 
        ILogger logger)
    {
        _eventsPendingConfirmation = new ConcurrentDictionary<PublishingKey, OutboxEvent>();
        SaveQueue = saveQueue;
        RetryQueue = retryQueue;
        _channelFactory = channelFactory;
        _listenQueue = listenQueue;
        _logger = logger;
        Channel = GetConfiguredChannel();
        DefaultMessageProperties = Channel!.CreateBasicProperties();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CancellationToken = stoppingToken;
        _logger.LogInformation("{CurrentTime}: Started Listening for messages to publish", DateTime.UtcNow);
        return PublishEventsFromQueue(stoppingToken);
    }

    private async Task PublishEventsFromQueue(CancellationToken cancellationToken)
    {
        var totalPublishedMessages = 0;
        await foreach(var @event in _listenQueue.GetAllAsync(cancellationToken))
        {
            if (totalPublishedMessages % 10_000 == 0 && totalPublishedMessages != 0)
            {
                _logger.LogInformation("{CurrentTime}: Messages published since start: {Count}",
                    DateTime.UtcNow, totalPublishedMessages);
            }

            // If the channel is closed save the status on the database
            // The client is auto-recovering
            if (Channel!.IsClosed)
            {
                _logger.LogError("{CurrentTime}: Channel closed, current message Id: {MessageId}",
                    DateTime.UtcNow, @event.Id);
                if (!_eventsPendingConfirmation.IsEmpty) _eventsPendingConfirmation.Clear();
                await SaveQueue.Enqueue(@event, cancellationToken);
                await Task.Delay(300, cancellationToken);
                continue;
            }

            try
            {
                var publishingKey = new PublishingKey(Channel.ChannelNumber, Channel.NextPublishSeqNo);
                await _eventsPendingConfirmation.AddWithRetries(publishingKey, @event, _logger);
                await PublishEvent(@event);
                unchecked
                {
                    totalPublishedMessages++;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,"{CurrentTime}: Error on publishing, current message Id: {MessageId}",
                    DateTime.UtcNow, @event.Id);
                await RecoverFromFailure(@event);
            }
        }
    }

    protected abstract Task PublishEvent(OutboxEvent @event);

    private IModel? GetConfiguredChannel()
    {
        try
        {
            TryDisposeChannel();
            var channel = _channelFactory.GetChannel();
            channel.ConfirmSelect();
            channel.BasicAcks += HandleAcks;
            channel.BasicNacks += HandleNacks;
            return channel;
        }
        catch (Exception)
        {
            _logger.LogCritical("{CurrentTime}: Cannot get a new channel", DateTime.UtcNow);
            return null;
        }
    }

    # region Failure Recovery

    private void TryDisposeChannel()
    {
        try
        {
            if (Channel is null) return;
            Channel.Close();
            Channel.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "{CurrentTime}: Error on channel disposal", DateTime.UtcNow);
        }
    }

    private async Task RecoverFromFailure(OutboxEvent currentEvent)
    {
        _logger.LogWarning("{CurrentTime}: Starting the failure recover", DateTime.UtcNow);

        if (Channel is null)
        {
            _logger.LogCritical("{CurrentTime}: Channel is null, trying to get a new one", DateTime.UtcNow);
            // try get a new channel
            Channel = GetConfiguredChannel();
            // If the channel still null, try save all events pending confirmation
            if (Channel is null)
            {
                _logger.LogCritical("{CurrentTime}: Cannot get a new channel", DateTime.UtcNow);
                await RedirectEventsDueFailure(SaveQueue, currentEvent);
                return;
            }

            // get a new instance of message properties
            DefaultMessageProperties = Channel.CreateBasicProperties();

            // If can get a new channel, try retry all events pending confirmation
            await RedirectEventsDueFailure(RetryQueue, currentEvent);

            return;
        }

        // If the channel is open and has an event to recovery, it could be:
        // - a problem with the concurrent dictionary used to track the published events
        // - or a problem with the event itself
        if (Channel.IsOpen)
        {
            _logger.LogWarning("{CurrentTime}: The channel is open, sending message {EventId} to retry queue",
                DateTime.UtcNow, currentEvent.Id);
            await RedirectEventDueFailure(RetryQueue, currentEvent);
        }
    }

    private async ValueTask RedirectEventDueFailure(IBaseQueueWriter destinationQueue, OutboxEvent @event)
    {
        // Try remove the current event from the pending confirmations
        var allKeys = _eventsPendingConfirmation.Keys.ToArray();
        foreach (var key in allKeys)
        {
            var found = _eventsPendingConfirmation.TryGetValue(key, out var possibleEvent);
            if (!found) continue;
            if (!possibleEvent!.Id.Equals(@event.Id)) continue;
            _eventsPendingConfirmation.TryRemove(key, out _);
            break;
        }

        // Try to send it to the retry queue
        await destinationQueue.TryEnqueue(@event, CancellationToken);
    }

    private async ValueTask RedirectEventsDueFailure(IBaseQueueWriter destinationQueue, OutboxEvent currentEvent)
    {
        // Take an snapshot of the keys
        var allKeys = _eventsPendingConfirmation.Keys.ToArray();
        // Try remove each key, an event could not exists anymore in the dict due to Handle Ack or Nack
        foreach (var key in allKeys)
        {
            var found = _eventsPendingConfirmation.TryRemove(key, out var eventFromDeadChannel);
            // If the event doesn't exists continue
            if (!found) continue;
            // If the removed event is the current, continue
            if (eventFromDeadChannel!.Id.Equals(currentEvent.Id)) continue;
            await destinationQueue.TryEnqueue(eventFromDeadChannel, CancellationToken);
        }

        // Redirect the current event
        await destinationQueue.TryEnqueue(currentEvent, CancellationToken);
    }

    #endregion

    #region EventHandlers

    // Acks are always redirected to the save queue
    private void HandleAcks(object? channel, BasicAckEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var key = new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag);
        var eventFound = _eventsPendingConfirmation.TryRemove(key, out var @event);
        if (!eventFound) return;
        @event!.SetPublishedStatus();
        SaveQueue.Enqueue(@event, CancellationToken.None).AsTask().Wait(CancellationToken);
    }

    // Nacks redirect responsibility is given to the derived class
    private void HandleNacks(object? channel, BasicNackEventArgs eventArgs)
    {
        if (channel is not IModel channelCasted) throw new Exception("It should be a channel");
        var key = new PublishingKey(channelCasted.ChannelNumber, eventArgs.DeliveryTag);
        var eventFound = _eventsPendingConfirmation.TryRemove(key, out var @event);
        if (!eventFound || @event is null) return;
        RedirectNackMessage(@event);
    }

    protected abstract void RedirectNackMessage(OutboxEvent @event);

    #endregion
}