namespace Kable.SharedMemory.Tests;

using Kable.SharedMemory.Waveform;
using Xunit;

public sealed class WaveformLifetimeTests
{
    [Fact]
    public void ZeroCapacityIsRejected()
    {
        Assert.Throws<ArgumentException>(() => SharedMemoryWaveformBuffer.Create(Guid.NewGuid().ToString("N"), 0));
    }

    [Fact]
    public void DuplicateCreateCannotResetExistingData()
    {
        string name = Guid.NewGuid().ToString("N");
        using var buffer = SharedMemoryWaveformBuffer.Create(name, 8);
        buffer.TryWrite(new WaveformSample(1, 42));
        Assert.Throws<IOException>(() => SharedMemoryWaveformBuffer.Create(name, 8));
        using var reader = SharedMemoryWaveformBuffer.Open(name);
        Assert.True(reader.TryRead(out var sample));
        Assert.Equal(42, sample.Value);
    }

    [Fact]
    public void OperationsAfterDisposeThrowBeforePointerAccess()
    {
        var buffer = SharedMemoryWaveformBuffer.Create(Guid.NewGuid().ToString("N"), 8);
        buffer.Dispose();
        buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => buffer.TryRead(out _));
        Assert.Throws<ObjectDisposedException>(() => buffer.TryWrite(new WaveformSample(1, 1)));
        Assert.Throws<ObjectDisposedException>(() => buffer.Close());
        Assert.Throws<ObjectDisposedException>(() => buffer.IsClosed);
    }

    [Fact]
    public async Task DisposeWakesWaiter()
    {
        var buffer = SharedMemoryWaveformBuffer.Create(Guid.NewGuid().ToString("N"), 8);
        var wait = Task.Run(() =>
        {
            try { buffer.WaitForData(Timeout.Infinite); }
            catch (ObjectDisposedException) { }
        });
        buffer.Dispose();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConcurrentDisposeCannotReleaseMemoryDuringAnOperation()
    {
        var buffer = SharedMemoryWaveformBuffer.Create(Guid.NewGuid().ToString("N"), 8);
        using var started = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            started.Set();
            try
            {
                for (int i = 0; i < 100000; i++)
                {
                    buffer.WriteBatch(new WaveformSample[] { new(1, i) });
                    buffer.ReadBatch(new WaveformSample[1]);
                }
            }
            catch (ObjectDisposedException) { }
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        buffer.Dispose();
        await writer.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
