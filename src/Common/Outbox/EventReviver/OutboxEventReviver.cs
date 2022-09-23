using Common.Outbox.EventRetry;
using Common.Outbox.Extensions;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.EventReviver;

public sealed class OutboxEventReviver : BackgroundService
{
    private readonly IDatabaseConnection _databaseConnection;
    private readonly IOutboxEventRetryQueue _retryQueue;
    private readonly ILogger<OutboxEventReviver> _logger;
    private readonly PeriodicTimer _timerToCheckDatabase;
    private readonly PeriodicTimer _timerToCleanDatabase;
    private const int MaxEventsFetched = 10_000;

    public OutboxEventReviver(
        IDatabaseConnection databaseConnection,
        IOutboxEventRetryQueue retryQueue,
        ILogger<OutboxEventReviver> logger)
    {
        _databaseConnection = databaseConnection;
        _retryQueue = retryQueue;
        _logger = logger;
        _timerToCheckDatabase = new PeriodicTimer(TimeSpan.FromMinutes(3));
        _timerToCleanDatabase = new PeriodicTimer(TimeSpan.FromHours(1));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started monitoring events to revive {CurrentTime}", DateTime.Now);
        return Task.WhenAny(CheckTheDatabaseForEventsToPublish(stoppingToken), CleanOldEvents(stoppingToken))
            .ReturnExceptions();
    }

    private async Task CheckTheDatabaseForEventsToPublish(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            await _timerToCheckDatabase.WaitForNextTickAsync(ctx);
            var command = new CommandDefinition($"{CountEventsToPublishQuery} {GetEventsToPublishQuery}", cancellationToken: ctx);

            var totalEventCount = 0;
            var eventsRevived = await _databaseConnection.WithConnection<IEnumerable<OutboxEvent>?>(async conn =>
            {
                var gridReader = await conn.QueryMultipleAsync(command);
                totalEventCount = await gridReader.ReadSingleAsync<int>();
                return totalEventCount == 0 ? null : (await gridReader.ReadAsync<OutboxEvent>()).AsList();
            });

            if (totalEventCount == 0) continue;
            await ReenqueeEvents(eventsRevived!, totalEventCount, ctx);
        }
    }

    private async Task ReenqueeEvents(IEnumerable<OutboxEvent> eventsRevived, int totalEventCount, CancellationToken ctx)
    {
        foreach (var outboxEvent in eventsRevived)
            await _retryQueue.Enqueue(outboxEvent, ctx);

        if(totalEventCount < MaxEventsFetched) return;

        // If there are more events on database keep getting it
        var currentEventCount = 0;
        do
        {
            var command = new CommandDefinition(GetEventsToPublishQuery, cancellationToken: ctx);
            var currentEventsRevived = await _databaseConnection
                .WithConnection<IEnumerable<OutboxEvent>?>(async conn =>
                    (await conn.QueryAsync<OutboxEvent>(command)).AsList());

            // An error has occurred on database level
            if (currentEventsRevived is null)
                break;

            var enumeratedCurrentEvents = currentEventsRevived as List<OutboxEvent> ?? currentEventsRevived.ToList();
            currentEventCount = enumeratedCurrentEvents.Count;

            foreach (var outboxEvent in enumeratedCurrentEvents)
                await _retryQueue.Enqueue(outboxEvent, ctx);
        } while (currentEventCount == MaxEventsFetched);
    }

    private async Task CleanOldEvents(CancellationToken ctx)
    {
        // await five seconds for the first check
        await Task.Delay(5_000, ctx);
        while (!ctx.IsCancellationRequested)
        {
            var command = new CommandDefinition(CleanOldEventsQuery, cancellationToken: ctx);

            await _databaseConnection.WithConnection(conn => conn.ExecuteAsync(command));

            await _timerToCleanDatabase.WaitForNextTickAsync(ctx);
        }
    }

    #region Queries

    private const string CountEventsToPublishQuery = @"
                        SELECT 
	                        COUNT(""Id"")
                        FROM 
                            ""OutboxEvents""
                        WHERE 
                            ""Status"" = 1
                            OR
                            (""Status"" = 3 AND ""Retries"" < 15)
                       ";

    private const string GetEventsToPublishQuery = @"
                        SELECT 
	                        ""Id"", ""EventKey"", ""EventData""
                        FROM 
                            ""OutboxEvents""
                        WHERE 
                            ""Status"" = 1
                            OR
                            (""Status"" = 3 AND ""Retries"" < 15)
                        LIMIT 10000
                       ";

    private const string CleanOldEventsQuery = @"DELETE FROM ""OutboxEvents"" WHERE ""ExpireAt"" < @CurrentDate;";

    #endregion
}