# What Kable manages beyond a basic .NET device query

Kable provides device-oriented APIs and lifecycle rules for connections, request/reply exchanges,
polling, events and recovery. Applications configure equipment addresses, commands and protocol
rules instead of rebuilding the shared communication behavior for each product.

This guide compares functionality and implementation responsibilities. The proposed module layout
is an example design, not a mandatory file count. Development time, lines saved and defect-reduction
percentages have not been measured and are not estimated here.

## 1. Querying once

These .NET 8/10 console examples query ASCII equipment with LF framing once.
Replace the endpoint and command with the device specification. Errors propagate to the caller.

### Basic .NET APIs

```csharp
using System.Net.Sockets;
using System.Text;

using var lifetime = new CancellationTokenSource();
using var client = new TcpClient { NoDelay = true };
await client.ConnectAsync("192.168.0.100", 9000, lifetime.Token);
await using var stream = client.GetStream();
using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
using var replyDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
replyDeadline.CancelAfter(TimeSpan.FromSeconds(2));

await stream.WriteAsync(Encoding.ASCII.GetBytes("STATUS?\n"), replyDeadline.Token);
string status = await reader.ReadLineAsync(replyDeadline.Token)
    ?? throw new EndOfStreamException("Device closed before replying.");
Console.WriteLine(status);
```

### Kable

Install the package with `dotnet add package Kable`.

```csharp
using Kable.Simple;

using var lifetime = new CancellationTokenSource();
await using var device = await KableSimple.OpenTcpAsync(
    "192.168.0.100", 9000, ct: lifetime.Token);
string status = await device.QueryAsync(
    "STATUS?", TimeSpan.FromSeconds(2), lifetime.Token);
Console.WriteLine(status);
```

Kable combines frame transmission, reply waiting and session lifetime management behind QueryAsync.
The basic example uses a cancellation deadline; Kable uses a request timeout. Their exception types
and reuse contracts therefore differ. Both examples are asynchronous and need no dedicated Thread
or Task.Run for ordinary communication.

The basic example closes after one query. Extending it to shared commands, polling and alarms
introduces the responsibilities below.
StreamReader already buffers lines and handles fragmented reads. A direct implementation can reuse
these building blocks; custom framing and device-request lifetimes still need coordinated design
and verification.

## 2. Responsibilities as requirements grow

| Requirement | Application work over basic APIs | Kable entry point | Device rules still configured by the app |
| --- | --- | --- | --- |
| Switch TCP, serial and Named Pipe | Transport-specific connection, I/O, cancellation and lifetime abstraction | Simple Open methods, IConnectionContext | Endpoints and serial settings |
| Split or coalesced incoming data | Partial-frame buffering, delimiters/lengths, consumption and size limits | Codecs and pipeline processing | Framing, encoding, maximum length |
| Queries from multiple callers | FIFO scheduling or ID registration, matching and cleanup | RequestAsync/QueryAsync, FIFO/CID routing | CID support, extraction and reuse |
| Alarms during queries | Classify frames and dispatch replies separately from events | Codec classification, LineReceived and streams | Device event recognition |
| Polling and fresh cached values | Timers, cancellation, worker lifetime, command cache and freshness | Profile polling, cache and TryGetFresh | Commands, intervals, acceptable age |
| Cancellation and delayed replies | Track request stages, remove waiters, prevent stale reply matching | FIFO session lifetime and CID request cleanup | Equipment-state and next-command decisions |
| Fail pending requests on disconnect | Detect connection termination, fail waiters, avoid double completion | Session termination and failure propagation | Application recovery behavior |
| Reconnect after an outage | Retry worker, session replacement, subscriptions and shutdown | Optional ReconnectingKableClient | Retry policy and equipment-state checks |
| Application shutdown | Stop producers, cancel/await workers, terminate pending work, release I/O | DisposeAsync and session cleanup | App-owned tasks and subscriptions |
| Diagnostics and UI integration | Capture traffic, deliver observations, separate UI from I/O | ICommObserver, optional WPF/OpenTelemetry packages | Retention and UI dispatch |

Choose an entry point rather than configuring every API: Simple for string queries, Profile for
polling and caching, or Builder with a codec for custom binary framing.

## 3. Why combining features needs coordination

Suppose polling and a manual query share one connection while an alarm arrives and the earlier
request is canceled. A complete implementation must coordinate all of the following:

1. Serialize requests or match them by CID.
2. Dispatch the alarm without consuming it as a normal reply.
3. Complete the canceled waiter and prevent its late reply from satisfying another request.
4. Terminate other pending work and polling consistently when the connection closes.
5. Restore communication through a new session without blindly replaying an earlier command.

The receive loop, timers, locks, response queues and recovery workers need a shared lifecycle and
error policy. Kable brings these shared behaviors into a library while the application supplies
device configuration and business decisions.

### Example responsibility layout over basic APIs

This is a proposed decomposition, not a newly implemented reference client.

```text
DeviceCommunication/
  Transports/
    ConnectionFactory.cs
    ConnectionLifetime.cs
  Codecs/
    FrameDecoder.cs
    FrameEncoder.cs
  Engine/
    RequestScheduler.cs
    ResponseRouter.cs
    RequestLifetime.cs
    EventDispatcher.cs
    PollingAndCache.cs
    RecoveryCoordinator.cs
    ShutdownAndDiagnostics.cs
```

For example, a response router must coordinate timeout removal with reply arrival so a pending
operation completes once. Protocol rules must distinguish older replies when IDs are reused.
Recovery and shutdown must also prevent a new connection from outliving application termination.

## 4. Configure polling, commands and cached values with Kable

```csharp
using Kable.Engine.Profiles;

var config = new KableProfileConfig()
    .UseTcp("192.168.0.100", 9000)
    .AddPeriodic("STATUS?", TimeSpan.FromSeconds(1))
    .AddCommand("ReadModel", "MODEL?");
await using var device = await KableProfileManager.ConnectAsync(config);
Action<string, string> onData = (command, value) =>
    Console.WriteLine($"{command}: {value}");
device.PeriodicDataReceived += onData;
try
{
    Console.WriteLine(await device.ExecuteAsync("ReadModel"));
    await Task.Delay(TimeSpan.FromSeconds(10));
    if (device.TryGetFresh("STATUS?", TimeSpan.FromSeconds(3), out var status))
        Console.WriteLine(status);
}
finally
{
    device.PeriodicDataReceived -= onData;
}
```

This ten-second demonstration registers commands and intervals, consumes events and checks a fresh
cached value. TryGetFresh can return false before the first polling reply. Implementing the same
behavior directly requires coordinated polling, request scheduling, reply dispatch, caching and
shutdown. See [getting started](GETTING_STARTED.md) for the entry-point contracts.

## 5. Verification required for an equivalent implementation

| Implemented behavior | Cases to verify |
| --- | --- |
| Framing | Fragmented frames, coalesced frames, invalid lengths and maximum sizes |
| Reply routing | Concurrent calls, out-of-order CID replies, mixed alarms and replies |
| Cancellation and deadlines | Before send, waiting for the gate, after send and late replies |
| Connection lifetime | Complete pending work on closure; disposal racing with I/O |
| Polling and cache | No initial value, stale data, slow consumers, queries concurrent with polling |
| Recovery | Session replacement, exhausted retries, shutdown during recovery, command replay prevention |

This is a verification plan for equivalent features, not a claim that the comparison run covered
every case. The [fault report](../ko/benchmarks/FaultComparison.md), written in Korean, lists the
scenarios actually executed. A basic .NET implementation with the same added protection policy
also met the common requirements. Kable provides these policies and integration points so each
application does not need to rebuild them.

Continue with [failures and recovery](ERROR_HANDLING.md), [session lifetime](CONNECTION_LIFECYCLE.md)
and the [measurement scope](../ko/PERFORMANCE_BASELINE.md), written in Korean.
