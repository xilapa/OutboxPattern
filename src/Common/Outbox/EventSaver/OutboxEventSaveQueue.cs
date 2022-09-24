using Common.Outbox.Base;
using Microsoft.Extensions.Logging;

namespace Common.Outbox.EventSaver;

public sealed class OutboxEventSaveQueue : BaseQueue, IOutboxEventSaveQueue
{
    // TODO: make capacity configurable
    public OutboxEventSaveQueue(ILogger<OutboxEventSaveQueue> logger) : base(20_000, logger)
    { }
}

public interface IOutboxEventSaveQueue : IBaseQueue
{ }