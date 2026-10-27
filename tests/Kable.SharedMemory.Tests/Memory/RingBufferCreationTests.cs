namespace Kable.SharedMemory.Tests.Memory;

using Kable.SharedMemory.Memory;
using Xunit;

public class RingBufferCreationTests
{
    [Theory]
    [InlineData(0, 123)]
    [InlineData(4, 999)]
    [InlineData(8, 0)]
    [InlineData(8, -1)]
    [InlineData(8, 3)]
    [InlineData(8, 65536)]
    [InlineData(12, 4095)]
    [InlineData(192, 7)]
    public void InvalidHeader_IsRejectedWithoutReadingPayload(int offset, int value)
    {
        using var raw = new RawRingMapping();
        if (offset == 4) raw.View.Write(offset, (ushort)value);
        else raw.View.Write(offset, value);
        Assert.Throws<InvalidDataException>(() => SharedMemoryRingBuffer.Open(raw.Name));
    }

    [Fact]
    public void ClosedMapping_CannotBecomeANewConnection()
    {
        using var raw = new RawRingMapping();
        raw.View.Write(192, 1);
        Assert.Throws<InvalidOperationException>(() => SharedMemoryRingBuffer.Open(raw.Name));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    public void DuplicateCreate_FailsWithoutChangingExistingData(int newCapacity)
    {
        string name = "duplicate_" + Guid.NewGuid().ToString("N");
        using var writer = SharedMemoryRingBuffer.Create(name, 256);
        using var reader = SharedMemoryRingBuffer.Open(name);
        Assert.Equal(1, writer.Write(new byte[] { 42 }));
        Assert.Throws<IOException>(() => SharedMemoryRingBuffer.Create(name, newCapacity));
        byte[] received = new byte[1];
        Assert.Equal(1, reader.Read(received));
        Assert.Equal(42, received[0]);
        Assert.False(writer.IsClosed);
    }

    [Fact]
    public void ClosedMappingStillHeld_CannotBeOverwritten()
    {
        string name = "held_" + Guid.NewGuid().ToString("N");
        using var ring = SharedMemoryRingBuffer.Create(name, 256);
        ring.Close();
        Assert.Throws<IOException>(() => SharedMemoryRingBuffer.Create(name, 256));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingEvent_FailsAndAllowsSubsequentValidCreation(bool dataEvent, bool spaceEvent)
    {
        string name = "events_" + Guid.NewGuid().ToString("N");
        using (var raw = new RawRingMapping(name, dataEvent, spaceEvent))
            Assert.Throws<WaitHandleCannotBeOpenedException>(() => SharedMemoryRingBuffer.Open(name));
        using var created = SharedMemoryRingBuffer.Create(name, 256);
        using var opened = SharedMemoryRingBuffer.Open(name);
        Assert.False(opened.IsClosed);
    }

    [Fact]
    public void FailedSecondClientOpen_DoesNotCloseExistingFirstRing()
    {
        string channel = "half_client_" + Guid.NewGuid().ToString("N");
        using var existing = SharedMemoryRingBuffer.Create(channel + "_s2c", 256);
        Assert.Throws<FileNotFoundException>(() => SharedMemoryTransport.ConnectClientEndpoint(channel));
        Assert.False(existing.IsClosed);
        Assert.Equal(1, existing.Write(new byte[] { 31 }));
    }

    [Fact]
    public void FailedSecondServerCreate_ReleasesOnlyItsNewFirstRing()
    {
        string channel = "half_server_" + Guid.NewGuid().ToString("N");
        using var existing = SharedMemoryRingBuffer.Create(channel + "_c2s", 256);
        Assert.Throws<IOException>(() => SharedMemoryTransport.CreateServerEndpoint(channel, 256));
        Assert.False(existing.IsClosed);
        using var first = SharedMemoryRingBuffer.Create(channel + "_s2c", 256);
        Assert.False(first.IsClosed);
    }

    [Fact]
    public void ExistingEvent_IsNotReusedByANewMapping()
    {
        string name = "event_collision_" + Guid.NewGuid().ToString("N");
        using var oldEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name + "_data_evt");
        Assert.Throws<IOException>(() => SharedMemoryRingBuffer.Create(name, 256));
        Assert.False(oldEvent.WaitOne(0));
    }
}
