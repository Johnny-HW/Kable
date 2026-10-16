# Connect your first device

Kable is an asynchronous device communication library for .NET. Start with a string query,
then add periodic polling, a custom codec or a protocol-specific client when needed.

## 1. Install and send one query

Add the package from your .NET application project directory. For a new app, first run `dotnet new console -n KableDemo` and `cd KableDemo`.

```bash
dotnet add package Kable
```

This example connects to real TCP equipment. Replace the address, port and command with
your device's specification. String examples use ASCII and LF (`\n`) framing by default.

```csharp
using Kable.Simple;

await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
string status = await device.QueryAsync("STATUS?");
Console.WriteLine(status);
```

`OpenTcpAsync` returns a connected client with its internal receive processing started.
No additional `StartAsync` call is needed. `QueryAsync` sends a request and returns its reply;
`await using` disposes the connection asynchronously when the scope ends.
Specify `delimiter` if the device requires another terminator, such as CR (`0x0D`).

Other transports expose the same query API.

```csharp
using Kable.Simple;

await using var serial = await KableSimple.OpenSerialAsync("COM3", baudRate: 115200);
string model = await serial.QueryAsync("MODEL?");
Console.WriteLine(model);
```

```csharp
using Kable.Simple;

await using var pipe = await KableSimple.OpenNamedPipeAsync("equipment_pipe");
Console.WriteLine(await pipe.QueryAsync("STATUS?"));
```

Without hardware, run the repository's local mock-device example.

```bash
dotnet run --project samples/Kable.QuickStart/Kable.QuickStart.csproj
```

## 2. Choose an entry point

| Task | Start with | Your configuration |
| --- | --- | --- |
| Send a string command and receive one line | `KableSimple` | Commands, endpoint and event classification |
| Poll values and keep a latest-value cache | `KableProfileManager` | Polling intervals and commands |
| Binary or device-specific framing | `KableClientBuilder<T>` and a codec | Message type, framing and CID rules |
| Modbus / MELSEC equipment | `Kable.Modbus` / `Kable.Melsec` | Device addressing and protocol options |
| Restore a connection after disconnection | `ReconnectingKableClient` | Retry limits and device-state decisions |

Builder sessions need `StartAsync()` after `Build()`. Simple Open and Profile Connect methods
return started clients. You do not need to configure all three approaches for one device.

## 3. Do I need a Thread or Task.Run for communication?

Ordinary connect, send, receive and query calls do not need either. Kable runs asynchronous
send, receive and response-dispatch loops internally. Each loop does not reserve its own OS thread.
`await` does not keep the calling thread occupied while waiting for asynchronous I/O.
Applications can move CPU-intensive response analysis to a separate worker when needed.

Keep a Task for application-specific repeated work, and cancel and await it during shutdown.
The 10-second duration below defines the demo lifetime; it does not indicate receive completion.

```csharp
using Kable.Simple;

using var lifetime = new CancellationTokenSource();
await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
Task polling = PollAsync(device, lifetime.Token);
try
{
    await Task.Delay(TimeSpan.FromSeconds(10));
}
finally
{
    lifetime.Cancel();
    try { await polling; }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
}

static async Task PollAsync(IKableSimpleClient device, CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await timer.WaitForNextTickAsync(ct))
        Console.WriteLine(await device.QueryAsync("STATUS?", ct: ct));
}
```

This example targets .NET 8/10 and propagates unexpected communication errors to its caller.
netstandard2.0 consumers can use a cancelable `Task.Delay` loop or Profile instead.

## 4. Use Profile for polling and caching

```csharp
using Kable.Engine.Profiles;

var config = new KableProfileConfig()
    .UseTcp("192.168.0.100", 9000)
    .AddPeriodic("STATUS?", TimeSpan.FromSeconds(1))
    .AddCommand("Initialize", "INIT");
await using var device = await KableProfileManager.ConnectAsync(config);
Action<string, string> onData = (command, value) => Console.WriteLine($"{command}: {value}");
device.PeriodicDataReceived += onData;
try
{
    Console.WriteLine(await device.ExecuteAsync("Initialize"));
    if (device.TryGetFresh("STATUS?", TimeSpan.FromSeconds(3), out var status))
        Console.WriteLine(status);
}
finally
{
    device.PeriodicDataReceived -= onData;
}
```

Check the equipment's actual `INIT` behavior before using it; this illustrates a command alias.
Before the first polling reply, `GetLatest` can return null and `TryGetFresh` can return false.
Elapsed polling intervals do not prove a response has arrived. If initial data is required,
use events and cache checks together with a deadline and disconnection handling.

## Continue

- [Configuration and events](en/DEVELOPER_USAGE.md): connection/query deadlines, UI dispatch and Builder.
- [Failures and shutdown](en/ERROR_HANDLING.md): actions after exceptions, recovery and cancellation.
- [Connection lifecycle](en/CONNECTION_LIFECYCLE.md): FIFO/CID behavior and session reuse.
- [Architecture](en/01_ARCHITECTURE_OVERVIEW.md): extending device protocols.
