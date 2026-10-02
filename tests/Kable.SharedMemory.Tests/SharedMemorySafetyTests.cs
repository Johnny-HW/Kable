namespace Kable.SharedMemory.Tests;

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.SharedMemory.Memory;
using Xunit;

public class SharedMemorySafetyTests
{
    [Fact]
    public void SharedMemoryRingBuffer_WrapAround_GuaranteesCorrectSequenceAndZeroLoss()
    {
        string channel = "test_wrap_" + Guid.NewGuid().ToString("N")[..8];
        const int capacity = 1024; // small power of two to trigger multiple wraps

        using var serverBuffer = SharedMemoryRingBuffer.Create(channel, capacity);
        using var clientBuffer = SharedMemoryRingBuffer.Open(channel);

        byte[] chunk = new byte[300];
        byte[] readBuf = new byte[300];

        // Perform 20 writes of 300 bytes (6000 bytes total, wrapping buffer ~6 times)
        for (int i = 0; i < 20; i++)
        {
            Array.Fill(chunk, (byte)(i % 255));
            int written = serverBuffer.Write(chunk);
            Assert.Equal(300, written);

            int read = clientBuffer.Read(readBuf);
            Assert.Equal(300, read);

            Assert.Equal(chunk, readBuf);
        }
    }

    [Fact]
    public void SharedMemoryRingBuffer_DisposedBuffer_ReleasesHandlesCleanly()
    {
        string channel = "test_disp_" + Guid.NewGuid().ToString("N")[..8];

        var serverBuffer = SharedMemoryRingBuffer.Create(channel, 1024);
        var clientBuffer = SharedMemoryRingBuffer.Open(channel);

        clientBuffer.Dispose();
        serverBuffer.Dispose();

        // Writing to disposed buffer should safely return 0 without throwing AccessViolationException
        Assert.Equal(0, serverBuffer.Write(new byte[10]));
    }

    [Fact]
    public void SharedMemoryRingBuffer_BufferSaturation_WaitForSpaceReturnsFalseWhenFull()
    {
        string channel = "test_sat_" + Guid.NewGuid().ToString("N")[..8];
        const int capacity = 512;

        using var serverBuffer = SharedMemoryRingBuffer.Create(channel, capacity);
        using var clientBuffer = SharedMemoryRingBuffer.Open(channel);

        // Fill buffer to capacity
        byte[] fillData = new byte[capacity];
        int written = serverBuffer.Write(fillData);
        Assert.Equal(capacity, written);

        // Next write should immediately return 0 without writing
        int overflowWrite = serverBuffer.Write(new byte[10]);
        Assert.Equal(0, overflowWrite);

        // WaitForSpace with 50ms timeout should timeout (return false)
        bool hasSpace = serverBuffer.WaitForSpace(50);
        Assert.False(hasSpace);

        // Consumer reads 100 bytes
        byte[] drain = new byte[100];
        int read = clientBuffer.Read(drain);
        Assert.Equal(100, read);

        // Now WaitForSpace should immediately succeed
        bool hasSpaceAfterDrain = serverBuffer.WaitForSpace(50);
        Assert.True(hasSpaceAfterDrain);
    }

    [Fact]
    public async Task SharedMemoryRingBuffer_ConcurrentReadWriteAndDispose_NeverThrowsAccessViolationOrCrashes()
    {
        // Stress test: Multiple threads writing and reading at high frequency
        // while Dispose is asynchronously called on another thread.
        // Must never throw AccessViolationException or unhandled crash.
        for (int run = 0; run < 10; run++)
        {
            string channel = "test_race_" + Guid.NewGuid().ToString("N")[..8];
            var serverBuffer = SharedMemoryRingBuffer.Create(channel, 4096);
            var clientBuffer = SharedMemoryRingBuffer.Open(channel);

            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            var writerTask = Task.Run(() =>
            {
                byte[] data = new byte[64];
                while (!token.IsCancellationRequested && !serverBuffer.IsClosed)
                {
                    try
                    {
                        serverBuffer.Write(data);
                    }
                    catch (ObjectDisposedException) { break; }
                }
            });

            var readerTask = Task.Run(() =>
            {
                byte[] readBuf = new byte[64];
                while (!token.IsCancellationRequested && !clientBuffer.IsClosed)
                {
                    try
                    {
                        clientBuffer.Read(readBuf);
                    }
                    catch (ObjectDisposedException) { break; }
                }
            });

            // Let them spin concurrently for a random short burst (1~5ms)
            await Task.Delay(Random.Shared.Next(1, 5));

            // Concurrently dispose both buffers from test thread
            clientBuffer.Dispose();
            serverBuffer.Dispose();

            cts.Cancel();

            await Task.WhenAll(writerTask, readerTask);

            // Re-verify that subsequent accesses cleanly return 0
            Assert.Equal(0, serverBuffer.Write(new byte[10]));
            Assert.Equal(0, clientBuffer.Read(new byte[10]));
        }
    }
}
