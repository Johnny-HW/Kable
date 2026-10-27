namespace Kable.SharedMemory.Tests.Memory;

using System.IO.MemoryMappedFiles;
using Kable.SharedMemory.Memory;

internal sealed class RawRingMapping : IDisposable
{
    internal string Name { get; }
    internal MemoryMappedViewAccessor View { get; }
    private readonly MemoryMappedFile _mapping;
    private readonly EventWaitHandle? _data;
    private readonly EventWaitHandle? _space;

    internal RawRingMapping(string? name = null, bool dataEvent = true, bool spaceEvent = true)
    {
        Name = name ?? "raw_" + Guid.NewGuid().ToString("N");
        _mapping = MemoryMappedFile.CreateNew(Name + "_mmf", 512);
        View = _mapping.CreateViewAccessor();
        if (dataEvent) _data = new EventWaitHandle(false, EventResetMode.AutoReset, Name + "_data_evt");
        if (spaceEvent) _space = new EventWaitHandle(false, EventResetMode.AutoReset, Name + "_space_evt");
        View.Write(0, MmfRingBufferHeader.ExpectedMagic);
        View.Write(4, MmfRingBufferHeader.CurrentVersion);
        View.Write(8, 256);
        View.Write(12, 255);
    }

    public void Dispose()
    {
        _space?.Dispose();
        _data?.Dispose();
        View.Dispose();
        _mapping.Dispose();
    }
}
