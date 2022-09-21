using Common.Outbox;
using Common.Outbox.EventPublisher;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Persistence;

public sealed class Context : DbContext
{
    private readonly IQueueWriter _queueWriter;
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

    public Context(IOptionsSnapshot<SenderSettings> senderSettings, IQueueWriter queueWriter)
    {
        _queueWriter = queueWriter;
        _senderSettings = senderSettings.Value;
    }

    public Context(SenderSettings senderSettings)
    {
        _senderSettings = senderSettings;
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        var outboxEvents =  this.GetOutboxEventsAndAddToTransaction();
        var result = await base.SaveChangesAsync(cancellationToken);
        await _queueWriter.EnqueueToOutbox(outboxEvents, cancellationToken);
        return result;
    }

    public DbSet<SomeEntity> SomeEntities { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }
}