namespace Kable.SharedMemory.Memory;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct MmfRingBufferHeader
{
    public const uint ExpectedMagic = 0x4B41424C; // 'KABL'
    public const ushort CurrentVersion = 1;

    [FieldOffset(0)]
    public uint Magic;

    [FieldOffset(4)]
    public ushort Version;

    [FieldOffset(8)]
    public int Capacity;

    [FieldOffset(12)]
    public int Mask;

    // Cache line 1: Producer Write Offset
    [FieldOffset(64)]
    public long Head;

    // Cache line 2: Consumer Read Offset
    [FieldOffset(128)]
    public long Tail;

    // Cache line 3: State flags (0: Active, 1: Closed/Aborted)
    [FieldOffset(192)]
    public int IsClosed;
}
