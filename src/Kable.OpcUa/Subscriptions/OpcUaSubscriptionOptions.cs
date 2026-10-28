namespace Kable.OpcUa.Subscriptions;

public sealed record OpcUaSubscriptionOptions
{
    public int PublishingIntervalMs { get; init; } = 250;
    public int SamplingIntervalMs { get; init; } = 100;
    public uint ServerQueueSize { get; init; } = 16;
    public int Capacity { get; init; } = 1024;
}
