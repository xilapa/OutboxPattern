using Common.Outbox;
using Common.Outbox.EventPublisher;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Persistence;

public sealed class Context : DbContext
{
    private readonly IOutboxEventQueue _outboxQueue;
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

    public Context(IOptions<SenderSettings> senderSettings, IOutboxEventQueue outboxQueue)
    {
        _outboxQueue = outboxQueue;
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
        // Create the outbox events
        var outboxEvents = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(domainEvent => new OutboxEvent(domainEvent))
            .ToArray();

        // Add them to the current transaction
        foreach (var outboxEvent in outboxEvents)
            Add(outboxEvent);

        // Call the base Save changes
        var result = await base.SaveChangesAsync(cancellationToken);

        // If the SaveChanges doesn't throw an exception this line will be called
        // And will send the outbox events to be published without hitting the database again
        foreach (var outboxEvent in outboxEvents)
            await _outboxQueue.Enqueue(outboxEvent, cancellationToken);

        return result;
    }

    public DbSet<SomeEntity> SomeEntities { get; set; }
    
    
    
    public DbSet<OutboxEvent> OutboxEvents { get; set; }
}