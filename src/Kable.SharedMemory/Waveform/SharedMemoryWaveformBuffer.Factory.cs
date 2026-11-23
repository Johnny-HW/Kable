namespace Kable.SharedMemory.Waveform;

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

public unsafe sealed partial class SharedMemoryWaveformBuffer
{
    /// <summary>Creates a new named buffer. An existing buffer is never reset; use Open to attach.</summary>
    public static SharedMemoryWaveformBuffer Create(string bufferName, int capacityPowerOfTwo = 16384)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bufferName);
        if (capacityPowerOfTwo <= 0 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            throw new ArgumentException("Capacity must be a positive power of two.", nameof(capacityPowerOfTwo));
        long totalBytes = 256 + (long)capacityPowerOfTwo * sizeof(WaveformSample);
        var (mmfName, evtName) = GetKernelObjectNames(bufferName);
        MemoryMappedFile mmf;
        try
        {
            mmf = MemoryMappedFile.CreateNew(mmfName, totalBytes, MemoryMappedFileAccess.ReadWrite);
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 183 or 80)
        {
            var duplicate = new IOException(
                $"Waveform buffer '{bufferName}' already exists; initialization stopped. " +
                "Check for a duplicate application instance or buffer name. " +
                "Use Open only when intentionally attaching to the existing buffer. Existing data was not changed.", error);
            duplicate.Data["ErrorCode"] = "DUPLICATE_WAVEFORM_BUFFER";
            duplicate.Data["BufferName"] = bufferName;
            throw duplicate;
        }
        return Attach(mmf, evtName, true, capacityPowerOfTwo, totalBytes);
    }

    public static SharedMemoryWaveformBuffer Open(string bufferName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bufferName);
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
            if (creator)
            {
                dataEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out bool createdNew);
                if (!createdNew)
                {
                    var duplicate = new IOException(
                        $"Waveform event '{eventName}' already exists; initialization stopped.");
                    duplicate.Data["ErrorCode"] = "DUPLICATE_WAVEFORM_EVENT";
                    throw duplicate;
                }
            }
            else
            {
                dataEvent = EventWaitHandle.OpenExisting(eventName);
            }
            return new SharedMemoryWaveformBuffer(mmf, accessor, pointer, dataEvent, creator, capacity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SharedMemoryWaveformBuffer] Attach cleanup: {ex.Message}");
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
