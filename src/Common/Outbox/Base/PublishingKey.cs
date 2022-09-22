namespace Common.Outbox.Base;

public record PublishingKey(int ChannelId, ulong MessageId);