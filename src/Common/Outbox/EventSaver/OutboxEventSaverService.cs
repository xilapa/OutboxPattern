using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Common.Outbox.EventSaver;

public sealed class OutboxEventSaverService : BackgroundService
{
    private readonly IOutboxEventSaveQueue _saveQueue;
    private readonly SenderSettings _senderSettings;
    private readonly ILogger<OutboxEventSaverService> _logger;
    private readonly List<OutboxEvent> _publishedEvents;
    private readonly List<OutboxEvent> _errorOnPublishingEvents;
    private readonly object _outboxEventsLock;

    public OutboxEventSaverService(
        IOutboxEventSaveQueue saveQueue,
        IOptions<SenderSettings> senderSettings,
        ILogger<OutboxEventSaverService> logger)
    {
        _saveQueue = saveQueue;
        _senderSettings = senderSettings.Value;
        _logger = logger;
        _publishedEvents = new List<OutboxEvent>(10200);
        _errorOnPublishingEvents = new List<OutboxEvent>(10200);
        _outboxEventsLock = new object();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{CurrentTime}: Started Listening for events to save", DateTime.UtcNow);
        return Task.WhenAny(StartListeningAsync(stoppingToken), RecurringSaver(stoppingToken));
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
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
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
            publishedEventsToSave = _publishedEvents.ToArray();
            errorOnPublishingEventsToSave = _errorOnPublishingEvents.ToArray();
            _publishedEvents.Clear();
            _errorOnPublishingEvents.Clear();
        }

        var commandParams = new
        {
            Date = DateTime.UtcNow,
            ExpireDateSuccess = DateTime.UtcNow.AddHours(3),
            ExpireDateError = DateTime.UtcNow.AddDays(2)
        };

        await using var connection = new NpgsqlConnection(_senderSettings.DatabaseConnectionString);

        if (publishedEventsToSave.Length > 0)
        {
            await connection.ExecuteAsync($@"UPDATE ""OutboxEvents"" 
                                                SET 
                                                ""Status"" = 2,
                                                ""Retries"" = ""Retries"" + 1,
                                                ""LastRetryDate"" = @Date,
                                                ""PublishingDate"" = @Date,
                                                ""ExpirationDate"" = @ExpireDateSuccess
                                                WHERE ""Id"" IN ({Utils.ConcatGuidsToQueryString(publishedEventsToSave.Select(_ => _.Id))});",
                commandParams);
        }

        if (errorOnPublishingEventsToSave.Length > 0)
        {
            await connection.ExecuteAsync($@"UPDATE ""OutboxEvents"" 
                                                SET 
                                                    ""Status"" = 3,
                                                    ""Retries"" = ""Retries"" + 1,
                                                    ""LastRetryDate"" = @Date,
                                                    ""ExpirationDate"" = @ExpireDateError
                                                WHERE ""Id"" IN ({Utils.ConcatGuidsToQueryString(publishedEventsToSave.Select(_ => _.Id))});",
                commandParams);
        }

        _logger.LogInformation("{CurrentTime}: Events updated in database Success: {SuccessCount} - Error: {ErrorCount}",
            DateTime.UtcNow, publishedEventsToSave.Length, errorOnPublishingEventsToSave.Length);
    }
}