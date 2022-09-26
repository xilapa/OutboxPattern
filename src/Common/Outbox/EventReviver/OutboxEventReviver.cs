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
        _timerToCheckDatabase = new PeriodicTimer(TimeSpan.FromMinutes(1));
        _timerToCleanDatabase = new PeriodicTimer(TimeSpan.FromHours(1));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{CurrentTime}: Started monitoring events to revive", DateTime.UtcNow);
        return Task.WhenAny(CheckTheDatabaseForEventsToPublish(stoppingToken), CleanOldEvents(stoppingToken))
            .ReturnExceptions();
    }

    private async Task CheckTheDatabaseForEventsToPublish(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            await _timerToCheckDatabase.WaitForNextTickAsync(ctx);
            await ReEnqueueEvents(ctx);
        }
    }

    // TODO: save last retries in memory to not repeat it for 5 minutes
    private async Task ReEnqueueEvents(CancellationToken ctx)
    {
        var totalEventsRevived = 0;
        var getMore = false;
        var offset = 0;
        do
        {
            var parameters = new
            {
                MaxEventsFetched,
                OffSet = offset
            };
            var command = new CommandDefinition(GetEventsToPublishQuery, parameters, cancellationToken: ctx);
            var currentEventsRevived = await _databaseConnection
                .WithConnection<List<OutboxEvent>?>(async conn =>
                    (await conn.QueryAsync<OutboxEvent>(command)).AsList());

            // An error has occurred on database level
            if (currentEventsRevived is null)
            {
                _logger.LogError("{CurrentTime}: Events stopped to be revived due database error", DateTime.UtcNow);
                break;
            }

            foreach (var @event in currentEventsRevived)
                await _retryQueue.Enqueue(@event, ctx);

            totalEventsRevived += currentEventsRevived.Count;
            offset += MaxEventsFetched;

            if (currentEventsRevived.Count < MaxEventsFetched)
                getMore = false;
        } while (getMore);

        _logger.LogInformation("{CurrentTime}: Events revived: {Count}", DateTime.UtcNow, totalEventsRevived);
    }

    private async Task CleanOldEvents(CancellationToken ctx)
    {
        // await five seconds for the first check
        await Task.Delay(5_000, ctx);
        while (!ctx.IsCancellationRequested)
        {
            var command = new CommandDefinition(CleanOldEventsQuery, new {CurrentDate = DateTime.UtcNow},
                cancellationToken: ctx);

            var cleanedCount =await _databaseConnection.WithConnection(conn => conn.ExecuteAsync(command));

            _logger.LogInformation("{CurrentTime}: Events cleaned: {Count}", DateTime.UtcNow, cleanedCount);

            await _timerToCleanDatabase.WaitForNextTickAsync(ctx);
        }
    }

    #region Queries

    private const string GetEventsToPublishQuery = @"
                        SELECT 
	                        ""Id"", ""EventKey"", ""EventData""
                        FROM 
                            ""OutboxEvents""
                        WHERE 
                            ""Status"" = 1
                            OR
                            (""Status"" = 3 AND ""Retries"" < 15)
                        ORDER BY ""EventDate"" ASC
                        LIMIT @MaxEventsFetched
                        OFFSET @OffSet";

    private const string CleanOldEventsQuery = @"DELETE FROM ""OutboxEvents"" WHERE ""ExpirationDate"" < @CurrentDate;";

    #endregion
}