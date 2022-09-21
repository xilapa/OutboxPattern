namespace Common.Outbox.EventPublisher;

public static class QueueWriterExtensions
{
    public static async Task EnqueueToOutbox(this IQueueWriter queueWriter, IEnumerable<OutboxEvent> outboxEvents,
        CancellationToken cancellationToken)
    {
        foreach (var outboxEvent in outboxEvents)
            await queueWriter.Enqueue(outboxEvent, cancellationToken);
    }
}