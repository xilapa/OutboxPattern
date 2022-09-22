using Common.Outbox;
using Common.Outbox.EventPublisher;
using Common.Outbox.Extensions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Persistence;

public sealed class Context : DbContext
{
    private readonly IOutboxEventQueueWriter _outboxEventQueueWriter;
    private readonly SenderSettings _senderSettings;

    // ctor for migration generation
    // public Context()
    // {
    //     _senderSettings = new SenderSettings
    //     {
    //         DatabaseConnectionString =
    //             "Server=localhost;Port=5432;Database=OutboxPattern;User Id=admin;Password=senha_123;"
    //     };
    // }

    public Context(IOptions<SenderSettings> senderSettings, IOutboxEventQueueWriter outboxEventQueueWriter)
    {
        _outboxEventQueueWriter = outboxEventQueueWriter;
        _senderSettings = senderSettings.Value;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SomeEntity>()
            .Ignore(_ => _.DomainEvents);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseNpgsql(_senderSettings.DatabaseConnectionString);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new ())
    {
        var outboxEvents =  this.GetOutboxEventsAndAddToTransaction();
        var result = await base.SaveChangesAsync(cancellationToken);
        await _outboxEventQueueWriter.EnqueueToOutbox(outboxEvents, cancellationToken);
        return result;
    }

    public DbSet<SomeEntity> SomeEntities { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }
}