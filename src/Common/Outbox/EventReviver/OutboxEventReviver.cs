using Common.Outbox.EventPublisher;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Common.Outbox.EventReviver;

public sealed class OutboxEventReviver : BackgroundService
{
    private readonly IOutboxEventQueue _eventQueue;
    private readonly SenderSettings _senderSettings;
    private readonly ILogger<OutboxEventReviver> _logger;
    private readonly PeriodicTimer _timerToCheckDatabase;

    public OutboxEventReviver(
        IOutboxEventQueue eventQueue,
        IOptions<SenderSettings> senderSettings,
        ILogger<OutboxEventReviver> logger)
    {
        _eventQueue = eventQueue;
        _senderSettings = senderSettings.Value;
        _logger = logger;
        _timerToCheckDatabase = new PeriodicTimer(TimeSpan.FromMinutes(5));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{CurrentTime}: Started monitoring events to revive", DateTime.UtcNow);
        return Task.WhenAny(CheckTheDatabaseForEventsToPublish(stoppingToken));
    }

    private async Task CheckTheDatabaseForEventsToPublish(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            await _timerToCheckDatabase.WaitForNextTickAsync(ctx);
            await ReEnqueueEvents(ctx);
        }
    }

    private async Task ReEnqueueEvents(CancellationToken ctx)
    {
        var totalEventsRevived = 0;
        var getMore = false;
        var offset = 0;
        do
        {
            var parameters = new
            {
                MaxEventsFetched = 10_000,
                OffSet = offset,
                MinRecoverDate = DateTime.UtcNow.Add(-1 * TimeSpan.FromMinutes(5))
            };

            var query = @"  SELECT 
                                ""Id"", ""EventKey"", ""EventData""
                            FROM 
                                ""OutboxEvents""
                            WHERE 
                                (
                                    (""Status"" = 1 AND ""EventDate"" < @MinRecoverDate)
                                OR
                                    (""Status"" = 3 AND ""Retries"" < 15 AND ""LastRetryDate"" < @MinRecoverDate) 
                                    )
                            ORDER BY ""EventDate"" ASC
                                LIMIT @MaxEventsFetched
                            OFFSET @OffSet";
            var command = new CommandDefinition(query, parameters, cancellationToken: ctx);
            await using var connection = new NpgsqlConnection(_senderSettings.DatabaseConnectionString);
            var currentEventsRevived = (await connection.QueryAsync<OutboxEvent>(command)).AsList();

            foreach (var @event in currentEventsRevived)
                await _eventQueue.Enqueue(@event, ctx);

            totalEventsRevived += currentEventsRevived.Count;
            offset += 10_000;

            if (currentEventsRevived.Count < 10_000)
                getMore = false;
        } while (getMore);

        _logger.LogInformation("{CurrentTime}: Events revived: {Count}", DateTime.UtcNow, totalEventsRevived);
    }
}