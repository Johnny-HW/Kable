using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using Kable.SharedMemory.Memory;
using Kable.SharedMemory.WaitDiagnostics;

if (args.Length != 2 || args[0] is not ("control" or "phases"))
    throw new ArgumentException("Expected control|phases iterations.");
int iterations = int.Parse(args[1]);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
ThreadPool.SetMinThreads(20, 20);
using var handle = new EventWaitHandle(false, EventResetMode.AutoReset);
using var cancellation = new CancellationTokenSource();
bool phases = args[0] == "phases";
var total = new Distribution();
var registration = new Distribution();
var cancellationSetup = new Distribution();
var callbackDelay = new Distribution();
var continuationDelay = new Distribution();
var cleanup = new Distribution();
var outerResume = new Distribution();
var sum = new Distribution();
for (int i = 0; i < 5000; i++) await RunOnceAsync(record: false);
GC.Collect();
GC.WaitForPendingFinalizers();
bool reserved = GC.TryStartNoGCRegion(128L * 1024 * 1024, disallowFullBlockingGC: true);
if (!reserved) throw new InvalidOperationException("Cannot reserve diagnostic no-GC budget.");
try
{
    long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    int collectionsBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
    for (int i = 0; i < iterations; i++) await RunOnceAsync(record: true);
    long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    bool validNoGc = GCSettings.LatencyMode == GCLatencyMode.NoGCRegion &&
        collectionsBefore == GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
    if (!validNoGc) throw new InvalidOperationException("GC occurred during the isolated wait measurement.");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Mode = args[0], Iterations = iterations, NoGc = validNoGc,
        AllocatedBytesPerWait = (double)allocated / iterations, Total = total.Summary(),
        Register = phases ? registration.Summary() : null,
        CancellationSetup = phases ? cancellationSetup.Summary() : null,
        SignalToCallback = phases ? callbackDelay.Summary() : null,
        CompletionToHelperResume = phases ? continuationDelay.Summary() : null,
        Unregister = phases ? cleanup.Summary() : null,
        HelperCleanupToCaller = phases ? outerResume.Summary() : null,
        MeasuredStageSum = phases ? sum.Summary() : null
    }));
}
finally { if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion) GC.EndNoGCRegion(); }

async Task RunOnceAsync(bool record)
{
    long start = Stopwatch.GetTimestamp();
    Task<bool> pending = RegisteredEventWait.WaitAsync(handle, Timeout.Infinite, cancellation.Token);
    WaitPhaseProbe? trace = WaitPhaseProbe.Current;
    if (phases && trace == null) throw new InvalidOperationException("The exported helper is not instrumented.");
    if (trace != null) trace.Signaled = Stopwatch.GetTimestamp();
    handle.Set();
    if (!await pending.ConfigureAwait(false)) throw new InvalidOperationException("The event was not observed.");
    long end = Stopwatch.GetTimestamp();
    if (!record) return;
    total.Add(end - start);
    if (!phases) return;
    registration.Add(trace!.RegisterEnd - trace.RegisterStart);
    cancellationSetup.Add(trace.CancellationRegistered - trace.RegisterEnd);
    callbackDelay.Add(trace.Callback - trace.Signaled);
    continuationDelay.Add(trace.Resumed - trace.Completing);
    cleanup.Add(trace.UnregisterEnd - trace.UnregisterStart);
    outerResume.Add(end - trace.UnregisterEnd);
    long measured = trace.RegisterEnd - trace.RegisterStart + trace.CancellationRegistered - trace.RegisterEnd +
        trace.Callback - trace.Signaled + trace.Resumed - trace.Completing +
        trace.UnregisterEnd - trace.UnregisterStart + end - trace.UnregisterEnd;
    sum.Add(measured);
}
