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
}
