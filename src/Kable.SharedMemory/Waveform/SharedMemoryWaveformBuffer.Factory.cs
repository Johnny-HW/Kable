namespace Kable.SharedMemory.Waveform;

using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

public unsafe sealed partial class SharedMemoryWaveformBuffer
{
    /// <summary>Creates a new named buffer. An existing buffer is never reset; use Open to attach.</summary>
    public static SharedMemoryWaveformBuffer Create(string bufferName, int capacityPowerOfTwo = 16384)
    {
        if (capacityPowerOfTwo <= 0 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            throw new ArgumentException("Capacity must be a positive power of two.", nameof(capacityPowerOfTwo));
        long totalBytes = 256 + (long)capacityPowerOfTwo * sizeof(WaveformSample);
        var (mmfName, evtName) = GetKernelObjectNames(bufferName);
        var mmf = MemoryMappedFile.CreateNew(mmfName, totalBytes, MemoryMappedFileAccess.ReadWrite);
        return Attach(mmf, evtName, true, capacityPowerOfTwo, totalBytes);
    }

    public static SharedMemoryWaveformBuffer Open(string bufferName)
    {
        var (mmfName, evtName) = GetKernelObjectNames(bufferName);
        var mmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.ReadWrite);
        return Attach(mmf, evtName, false, 0, 0);
    }

    private static SharedMemoryWaveformBuffer Attach(MemoryMappedFile mmf, string eventName,
        bool creator, int capacity, long length)
    {
        MemoryMappedViewAccessor? accessor = null;
        EventWaitHandle? dataEvent = null;
        byte* pointer = null;
        bool acquired = false;
        try
        {
            accessor = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            if (accessor.Capacity < 256) throw new InvalidOperationException("Waveform header is incomplete.");
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            acquired = true;
            dataEvent = creator
                ? new EventWaitHandle(false, EventResetMode.AutoReset, eventName)
                : EventWaitHandle.OpenExisting(eventName);
            return new SharedMemoryWaveformBuffer(mmf, accessor, pointer, dataEvent, creator, capacity);
        }
        catch
        {
            dataEvent?.Dispose();
            if (acquired) accessor!.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor?.Dispose();
            mmf.Dispose();
            throw;
        }
    }

    private static (string MmfName, string EvtName) GetKernelObjectNames(string name)
        => ($"{name}_wave_mmf", $"{name}_wave_evt");
}
