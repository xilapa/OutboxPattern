using Domain.Entities;
using Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Persistence;

public sealed class Context : DbContext
{
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

    public Context(IOptionsSnapshot<SenderSettings> senderSettings)
    {
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
            .Ignore(_ => _.BaseEvents);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseNpgsql(_senderSettings.DatabaseConnectionString);
    }

    public DbSet<SomeEntity> SomeEntities { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }
}