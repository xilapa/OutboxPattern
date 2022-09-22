using Common.Outbox.Base;

namespace Common.Outbox.Extensions;

public static class QueueWriterExtensions
{
    public static async Task EnqueueToOutbox(this IBaseQueueWriter baseQueueWriter, IEnumerable<OutboxEvent> outboxEvents,
        CancellationToken cancellationToken)
    {
        foreach (var outboxEvent in outboxEvents)
            await baseQueueWriter.Enqueue(outboxEvent, cancellationToken);
    }
}