namespace Kable.Tests.Cases.Observability;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Xunit;

public sealed class PcapObserverLifecycleTests
{
    private static PacketTraceRecord Trace(byte[] bytes, TrafficKind kind = TrafficKind.AperiodicCommand) =>
        new(DateTime.UtcNow, PacketDirection.Rx, kind, "TEST", bytes, null, TimeSpan.Zero);

    [Fact]
    public async Task Dispose_DrainsOwnedSnapshotsInOrder_AndCompletesReaders()
    {
        var stream = new ControlledPcapStream();
        var observer = new PcapStreamObserver(stream, new());
        byte[] bytes = [1, 2];
        observer.OnPacketTrace(Trace(bytes));
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        bytes[0] = 99;
        observer.OnPacketTrace(Trace([3, 4]));
        var first = observer.DisposeAsync().AsTask();
        var second = observer.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted);
        stream.Release.TrySetResult(true);
        await Task.WhenAll(first, second);
        await observer.Completion;
        var data = stream.ToArray();
        Assert.Equal(1, data[40]);
        Assert.Equal(3, data[58]);
        while (observer.CommandStream.TryRead(out _)) { }
        await observer.CommandStream.Completion;
        await observer.AlarmStream.Completion;
        await observer.PeriodicStream.Completion;
        Assert.Equal(1, stream.DisposeCount);
        observer.OnPacketTrace(Trace([5]));
        Assert.False(observer.CommandStream.TryRead(out _));
    }

    [Fact]
    public async Task Overflow_FailsRecordingWithoutThrowingOnProducer()
    {
        var stream = new ControlledPcapStream();
        var observer = new PcapStreamObserver(stream, new() { RecordingCapacity = 1 });
        observer.OnPacketTrace(Trace([1]));
        await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observer.OnPacketTrace(Trace([2]));
        observer.OnPacketTrace(Trace([3]));
        Assert.IsType<IOException>(observer.LastError);
        observer.OnPacketTrace(Trace([4]));
        stream.Release.TrySetResult(true);
        await Assert.ThrowsAsync<IOException>(() => observer.Completion);
        await Assert.ThrowsAsync<IOException>(() => observer.DisposeAsync().AsTask());
        while (observer.CommandStream.TryRead(out _)) { }
        await Assert.ThrowsAsync<IOException>(() => observer.CommandStream.Completion);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task DiskFailure_IsObservable_AndDisposesStream()
    {
        var stream = new ControlledPcapStream { FailWrite = true };
        stream.Release.TrySetResult(true);
        var observer = new PcapStreamObserver(stream, new());
        observer.OnPacketTrace(Trace([1]));
        await Assert.ThrowsAsync<IOException>(() => observer.Completion);
        Assert.IsType<IOException>(observer.LastError);
        await Assert.ThrowsAsync<IOException>(() => observer.DisposeAsync().AsTask());
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task UnconsumedStreams_AreBoundedAndCountOldestDrops()
    {
        var stream = new ControlledPcapStream();
        var observer = new PcapStreamObserver(stream, new()
        {
            CommandCapacity = 1, PeriodicCapacity = 1, AlarmCapacity = 1
        });
        foreach (var kind in Enum.GetValues<TrafficKind>())
        {
            observer.OnPacketTrace(Trace([1], kind));
            observer.OnPacketTrace(Trace([2], kind));
        }
        Assert.Equal(1, observer.DroppedCommandCount);
        Assert.Equal(1, observer.DroppedPeriodicCount);
        Assert.Equal(1, observer.DroppedAlarmCount);
        Assert.True(observer.CommandStream.TryRead(out var last));
        Assert.Equal(2, last.RawBytes.Span[0]);
        Assert.False(observer.CommandStream.TryRead(out _));
        stream.Release.TrySetResult(true);
        await observer.DisposeAsync();
    }

    [Fact]
    public async Task Snapshot_TruncatesCaptureButRetainsOriginalLength()
    {
        var stream = new ControlledPcapStream();
        stream.Release.TrySetResult(true);
        var observer = new PcapStreamObserver(stream, new());
        observer.OnPacketTrace(Trace(new byte[70000]));
        await observer.DisposeAsync();
        var data = stream.ToArray();
        Assert.Equal(65535u, BitConverter.ToUInt32(data, 32));
        Assert.Equal(70000u, BitConverter.ToUInt32(data, 36));
        Assert.Equal(24 + 16 + 65535, data.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidCapacity_IsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcapStreamObserver(
            new MemoryStream(), new() { RecordingCapacity = capacity }));
    }
}
