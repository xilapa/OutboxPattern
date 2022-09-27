using System.Threading.Channels;
using Common.Outbox.Base;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.EventRetry;

public sealed class OutboxEventRetryQueue : BaseQueue, IOutboxEventRetryQueue
{
    public OutboxEventRetryQueue(ILogger<OutboxEventRetryQueue> logger)
    // If the channel is full, drop the message. It'll be re-queued later by the EventReviver
        : base(20_000, logger, BoundedChannelFullMode.DropWrite)
    { }
}

public interface IOutboxEventRetryQueue : IBaseQueue
{ }