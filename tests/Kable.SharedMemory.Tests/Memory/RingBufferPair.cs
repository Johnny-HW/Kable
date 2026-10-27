namespace Kable.SharedMemory.Tests.Memory;

using Kable.SharedMemory.Memory;

internal sealed class RingBufferPair : IDisposable
{
    internal SharedMemoryRingBuffer Writer { get; }
    internal SharedMemoryRingBuffer Reader { get; }

    internal RingBufferPair()
    {
        string name = "wait_" + Guid.NewGuid().ToString("N");
        Writer = SharedMemoryRingBuffer.Create(name, 256);
        Reader = SharedMemoryRingBuffer.Open(name);
    }

    internal void Prepare(bool space)
    {
        if (space) Writer.Write(new byte[Writer.Capacity]);
    }

    internal ValueTask<bool> WaitAsync(bool space, int timeout = Timeout.Infinite, CancellationToken ct = default)
        => space ? Writer.WaitForSpaceAsync(timeout, ct) : Reader.WaitForDataAsync(timeout, ct);

    internal void Signal(bool space)
    {
        if (space) Reader.Read(new byte[1]);
        else Writer.Write(new byte[] { 42 });
    }

    internal void ClosePeer(bool space)
    {
        if (space) Reader.Close();
        else Writer.Close();
    }

    internal void DisposeWaiter(bool space)
    {
        if (space) Writer.Dispose();
        else Reader.Dispose();
    }

    public void Dispose()
    {
        Reader.Dispose();
        Writer.Dispose();
    }
}
