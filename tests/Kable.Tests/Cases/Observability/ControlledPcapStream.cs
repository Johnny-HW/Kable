namespace Kable.Tests.Cases.Observability;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal sealed class ControlledPcapStream : MemoryStream
{
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool FailWrite { get; set; }
    public int DisposeCount { get; private set; }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        Entered.TrySetResult(true);
        await Release.Task.WaitAsync(ct);
        if (FailWrite) throw new IOException("Injected disk failure.");
        await base.WriteAsync(buffer, offset, count, ct);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeCount++;
        base.Dispose(disposing);
    }
}
