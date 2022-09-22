using System.Text.Json;

namespace Common.Outbox;

public sealed class OutboxEvent
{
    // Ctor for EF Core
    // ReSharper disable once UnusedMember.Local
    private OutboxEvent()
    { }

    public OutboxEvent(object @event)
    {
        Id = Guid.NewGuid();
        EventKey = @event.GetType().Name;
        EventData = JsonSerializer.Serialize(@event);
        EventDate = DateTime.UtcNow;
        PublishingDate = null;
        LastRetryDate = null;
        Status = PublishingStatus.NotPublished;
        Retries = 0;
        CurrentRetries = 0;
        ExpirationDate = null;
    }

    public Guid Id { get; private set; }
    public string EventKey { get; private set; }
    public string EventData { get; private set; }
    public DateTime EventDate { get; private set; }
    public DateTime? PublishingDate { get; private set; }
    public DateTime? LastRetryDate { get; private set; }
    public PublishingStatus Status { get; private set; }
    public int Retries { get; private set; }
    public int CurrentRetries { get; private set; }
    public DateTime? ExpirationDate { get; set; }

    public void SetPublishedStatus()
    {
        Status = PublishingStatus.Published;
    }

    public void SetErrOnPublishStatus()
    {
        Status = PublishingStatus.ErrorOnPublish;
    }

    public void IncrementRetryCount()
    {
        CurrentRetries++;
    }
}

public enum PublishingStatus
{
    NotPublished = 1,
    Published,
    ErrorOnPublish
}
