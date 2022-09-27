using Common.Persistence;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sender;

public class WorkGenerator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkGenerator> _logger;

    public WorkGenerator(IServiceScopeFactory scopeFactory, ILogger<WorkGenerator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return SimulateAspNetRequests(stoppingToken);
    }

    private async Task SimulateAspNetRequests(CancellationToken cancellationToken)
    {
        await Task.Delay(1_000, cancellationToken);
        _logger.LogInformation("{CurrentTime} Started simulating AspNet requests", DateTime.UtcNow);
        for (var count = 0; count < 100_000; count++)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<Context>();
            await Task.Delay(300, cancellationToken);
            var someEntity = new SomeEntity($"Iteration {count}");
            context.Add(someEntity);
            await context.SaveChangesAsync(cancellationToken);
            if(count % 10_000 == 0 && count != 0)
                _logger.LogInformation("{CurrentTime}: {Count} requests generated", DateTime.UtcNow, count);
        }
    }
}