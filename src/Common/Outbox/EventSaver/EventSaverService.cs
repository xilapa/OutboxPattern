using Common.Outbox.Base;
using Common.Outbox.Extensions;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.EventSaver;

public sealed class EventSaverService : BackgroundService
{
    private readonly IOutboxEventSaveQueue _saveQueue;
    private readonly IDatabaseConnection _databaseConnection;
    private readonly ILogger<EventSaverService> _logger;
    private readonly List<OutboxEvent> _publishedEvents;
    private readonly List<OutboxEvent> _errorOnPublishingEvents;
    private readonly object _outboxEventsLock;
    private readonly DelayableTimer _checkItemsToSaveInterval;

    public EventSaverService(
        IOutboxEventSaveQueue saveQueue,
        IDatabaseConnection databaseConnection,
        ILogger<EventSaverService> logger)
    {
        _saveQueue = saveQueue;
        _databaseConnection = databaseConnection;
        _logger = logger;
        _publishedEvents = new List<OutboxEvent>(10200);
        _errorOnPublishingEvents = new List<OutboxEvent>(10200);
        _outboxEventsLock = new object();
        _checkItemsToSaveInterval = new DelayableTimer(TimeSpan.FromMinutes(1));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{CurrentTime}: Started Listening for events to save", DateTime.UtcNow);
        return Task.WhenAny(StartListeningAsync(stoppingToken), RecurringSaver(stoppingToken)).ReturnExceptions();
    }

    private async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        await foreach (var @event in _saveQueue.GetAllAsync(cancellationToken))
        {
            lock (_outboxEventsLock)
            {
                if (PublishingStatus.Published.Equals(@event.Status))
                {
                    _publishedEvents.Add(@event);
                }
                else
                {
                    @event.SetErrOnPublishStatus();
                    _errorOnPublishingEvents.Add(@event);
                }
            }

            // Just reading the collection size without synchronization
            // ReSharper disable once InconsistentlySynchronizedField
            if (_publishedEvents.Count + _errorOnPublishingEvents.Count >= 10_000)
                await SaveToDatabase();
        }
    }

    private async Task RecurringSaver(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _checkItemsToSaveInterval.WaitForNextTickAsync(cancellationToken);
            await SaveToDatabase();
        }
    }

    private async Task SaveToDatabase()
    {
        OutboxEvent[] publishedEventsToSave;
        OutboxEvent[] errorOnPublishingEventsToSave;

        // Get the events from the synchronized list
        lock (_outboxEventsLock)
        {
            if (_publishedEvents.Count == 0 && _errorOnPublishingEvents.Count == 0) return;
            _checkItemsToSaveInterval.Delay();
            publishedEventsToSave = _publishedEvents.ToArray();
            errorOnPublishingEventsToSave = _errorOnPublishingEvents.ToArray();
            _publishedEvents.Clear();
            _errorOnPublishingEvents.Clear();
        }

        var queryPublished = publishedEventsToSave.Length == 0 ? string.Empty
            : $"{QueryPublishedPartial}{Utils.ConcatGuidsToQueryString(publishedEventsToSave.Select(_ => _.Id))});";

        var queryErrorOnPublishing = errorOnPublishingEventsToSave.Length == 0
            ? string.Empty
            : $"{QueryErrorOnPublishingPartial}{Utils.ConcatGuidsToQueryString(errorOnPublishingEventsToSave.Select(_ => _.Id))});";

        var commandParams = new
        {
            Date = DateTime.UtcNow,
            ExpireDateSuccess = DateTime.UtcNow.AddHours(3),
            ExpireDateError = DateTime.UtcNow.AddDays(2)
        };

        await _databaseConnection.WithConnection(conn =>
            conn.ExecuteAsync($"{queryPublished} {queryErrorOnPublishing}", commandParams));

        _logger.LogInformation("{CurrentTime}: Events updated in database Success: {SuccessCount} - Error: {ErrorCount}",
            DateTime.UtcNow, publishedEventsToSave.Length, errorOnPublishingEventsToSave.Length);
    }

    #region PartialQueries
    private const string QueryPublishedPartial = @"UPDATE ""OutboxEvents"" 
                                                SET 
                                                ""Status"" = 2,
                                                ""Retries"" = ""Retries"" + 1,
                                                ""LastRetryDate"" = @Date,
                                                ""PublishingDate"" = @Date,
                                                ""ExpirationDate"" = @ExpireDateSuccess
                                                WHERE ""Id"" IN (";

    private const string QueryErrorOnPublishingPartial = @"UPDATE ""OutboxEvents"" 
                                                SET 
                                                    ""Status"" = 3,
                                                    ""Retries"" = ""Retries"" + 1,
                                                    ""LastRetryDate"" = @Date,
                                                    ""ExpirationDate"" = @ExpireDateError
                                                WHERE ""Id"" IN (";
    #endregion
}