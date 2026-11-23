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
        var error = Assert.Throws<IOException>(() => SharedMemoryWaveformBuffer.Create(name, 8));
        Assert.Contains(name, error.Message);
        Assert.Contains("initialization stopped", error.Message);
        Assert.Contains("Open", error.Message);
        Assert.Equal("DUPLICATE_WAVEFORM_BUFFER", error.Data["ErrorCode"]);
        Assert.IsType<IOException>(error.InnerException);
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

    [Fact]
    public void WaitForData_IgnoresStaleSignal_WhenBufferIsEmpty()
    {
        string name = Guid.NewGuid().ToString("N");
        using var buffer = SharedMemoryWaveformBuffer.Create(name, 8);
        Assert.True(buffer.TryWrite(new WaveformSample(1, 3.14)));
        Assert.True(buffer.TryRead(out _));

        // _dataEvent still had a pending AutoReset signal from TryWrite; WaitForData must re-check Head > Tail.
        Assert.False(buffer.WaitForData(40));
    }

    [Fact]
    public async Task WaitForData_ConcurrentDisposeStress_CompletesCleanlyWithoutHandleErrors()
    {
        for (int i = 0; i < 20; i++)
        {
            var buffer = SharedMemoryWaveformBuffer.Create(Guid.NewGuid().ToString("N"), 8);
            using var entered = new ManualResetEventSlim();
            var waiter = Task.Run(() =>
            {
                entered.Set();
                try { buffer.WaitForData(Timeout.Infinite); }
                catch (ObjectDisposedException) { }
            });
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            buffer.Dispose();
            await waiter.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(0L, 100L)]
    [InlineData(1024L, 0L)]
    [InlineData(long.MinValue, long.MaxValue)]
    public void CorruptedHeadTail_ReturnsFalseOrZeroAndClosesBuffer(long corruptHead, long corruptTail)
    {
        string name = "wave_corrupt_" + Guid.NewGuid().ToString("N")[..8];
        using var buffer = SharedMemoryWaveformBuffer.Create(name, 16);

        using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(name + "_wave_mmf",
            System.IO.MemoryMappedFiles.MemoryMappedFileRights.ReadWrite))
        using (var accessor = mmf.CreateViewAccessor(0, 256))
        {
            accessor.Write(64, corruptHead);
            accessor.Write(128, corruptTail);
        }

        Assert.False(buffer.TryWrite(new WaveformSample(1, 1.0)));
        Assert.True(buffer.IsClosed);
        Assert.Equal(0, buffer.WriteBatch(new WaveformSample[] { new(1, 2.0) }));
        Assert.False(buffer.TryRead(out _));
        Assert.Equal(0, buffer.ReadBatch(new WaveformSample[4]));
    }

    [Fact]
    public void Create_PreExistingEventObject_FailsWithDuplicateEventError()
    {
        string name = "wave_ev_" + Guid.NewGuid().ToString("N")[..8];
        using var existing = new EventWaitHandle(false, EventResetMode.AutoReset, name + "_wave_evt");

        var error = Assert.Throws<IOException>(() => SharedMemoryWaveformBuffer.Create(name, 8));
        Assert.Equal("DUPLICATE_WAVEFORM_EVENT", error.Data["ErrorCode"]);
    }
}
