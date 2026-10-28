# Hosting a multi-client server

Kable 1.10.0 adds `IKableServer<TMessage>`, `IServerSession<TMessage>` and
`KableServerBuilder<TMessage>` in the existing Kable/Core packages.
The transport → codec → engine boundaries remain unchanged.

## Start a TCP server

```csharp
using System.Net;
using Kable.Builder;
using Kable.Codecs;

await using var server = new KableServerBuilder<string>()
    .ListenTcp(IPAddress.Loopback, 9000)
    .UseCodec(() => new AsciiLineCodec())
    .ConfigureOptions(options =>
    {
        options.MaxConnections = 100;
        options.SessionQueueCapacity = 256;
        options.DefaultBroadcastTimeout = TimeSpan.FromSeconds(1);
        options.DefaultShutdownTimeout = TimeSpan.FromSeconds(3);
    })
    .Build();

server.ClientConnected += (_, e) => Console.WriteLine($"Connected: {e.Session.ConnectionId}");
server.MessageReceived += (_, e) => Console.WriteLine($"{e.Session.ConnectionId}: {e.Message}");
server.ClientDisconnected += (_, e) => Console.WriteLine($"Disconnected: {e.Reason}");
await server.StartAsync();
Console.ReadLine();
await server.StopAsync();
```

Build binds the TCP endpoint; StartAsync begins accepting connections. Configure and build once.
Use `WithListener` for an existing `IConnectionListener`, including IPC listeners.
The built server owns and disposes its listener and accepted connections.
The codec factory must return a new codec per connection; encode and decode may run concurrently.
For a dynamically assigned port, create `TcpConnectionListener(IPAddress.Loopback, 0)`, read
`LocalEndPoint`, then pass that listener to the builder.

## Address a client or broadcast

```csharp
if (server.TryGetSession(connectionId, out var session) && session != null)
    await session.SendAsync("STATUS", ct);
// Equivalent lookup and enqueue; missing connection IDs throw KeyNotFoundException.
await server.SendAsync(connectionId, "STATUS", ct);
var result = await server.BroadcastAsync("NOTICE", ct);
Console.WriteLine($"Queued: {result.EnqueuedCount}/{result.TotalSessions}");
foreach (var failedId in result.FailedConnectionIds)
    Console.WriteLine($"Not queued: {failedId}");
```

`SendAsync` completes when accepted into the session's bounded queue. It does not acknowledge
network delivery or equipment execution. `BroadcastResult` counts queue acceptance against a
snapshot of sessions; connections may change during the operation. Messages already queued
remain queued if the caller subsequently cancels. A broadcast deadline records failed IDs;
caller cancellation throws OperationCanceledException and does not undo partial acceptance.
Stopped servers reject SendAsync and BroadcastAsync. Sending directly through a stopped session
also fails. ActiveSessions returns a snapshot; TryGetSession cannot guarantee a future send succeeds.

## Capacity, failures and shutdown

| Option | Default | Meaning |
| --- | --- | --- |
| MaxConnections | 10,000 | Excess accepted connections are closed before codec creation |
| SessionQueueCapacity | 1,000 | Exact waiting-message count per connection; one more may be in flight |
| DefaultBroadcastTimeout | 1 second | Shared deadline for queue acceptance across the snapshot |
| DefaultShutdownTimeout | 3 seconds | Outgoing drain deadline before aborting remaining connections |

Options are validated and copied at construction. Capacities must be positive; timeouts must be
positive and at most Int32.MaxValue milliseconds. The queues are bounded by message count,
not payload bytes. String decoding, event arguments, session snapshots and broadcasts allocate;
the complete server path is not allocation-free.

StopAsync stops acceptance, rejects new sends, drains accepted messages, then disconnects.
A blocked flush is aborted at the drain deadline. Transport cleanup follows and can extend the
elapsed time: custom listeners must unblock AcceptAsync on Stop, and contexts must unblock I/O
on Abort and complete DisposeAsync. Cancellation of a StopAsync caller cancels only that caller's
wait; shared cleanup continues. Concurrent stop/dispose calls share cleanup; DisposeAsync awaits
it and disposes the listener. Stop before Start also ends the server's lifetime. Create a new
server after stopping. StartAsync's token checks cancellation at entry; it does not own the running
server's lifetime.

Callbacks run on background paths. Keep them short; marshal UI work through the UI dispatcher.
Do not synchronously wait for StopAsync/DisposeAsync inside callbacks. Exceptions from one
subscriber are traced and do not skip the remaining subscribers. Async-void handlers are not
awaited or protected; use an application-owned bounded worker for asynchronous message handling.
Codec failures disconnect only the affected session, with a reason in ClientDisconnected.
An accept failure is traced and stops the server. Observe IsRunning and lifecycle events.
There is no server request/reply correlation, authentication or TLS negotiation in this builder;
choose an appropriate custom transport and protocol for those requirements.

`PacketTraceRecord` gains an explicit connection-ID constructor and nullable ConnectionId property.
The existing constructor remains available and sets ConnectionId to null. The server does not
automatically emit packet observer records.

The compiled package consumer in `tools/release/Consumers/Console/ServerProbe.cs` verifies real
TCP receive, broadcast and shutdown using the packaged API on .NET 8 and .NET 10.
