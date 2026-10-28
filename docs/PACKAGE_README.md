# Kable

Asynchronous .NET equipment communication: transports, protocol framing and request/response sessions.
This package is part of Kable; install only the modules your application needs and align their versions.

## Packages and runtime support

- **Kable / Kable.Core**: netstandard2.0, .NET 8 and .NET 10. TCP, Serial, Named Pipe, TLS and session APIs.
- **Kable.Modbus / Kable.Mqtt / Kable.OpcUa**: .NET 8 and .NET 10. Industrial protocols and integration.
- **Kable.Generators / Kable.ProtocolPack**: netstandard2.0. C# source generation and YAML protocol definitions.
- **Kable.UI.Wpf**: .NET 8/10 for Windows. **Kable.SharedMemory**: .NET 10 for Windows.
- Other modules: inspect the package framework assets for exact target support.

```csharp
using Kable.Simple;
await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
Console.WriteLine(await device.QueryAsync("STATUS?"));
```

Strings default to ASCII with LF framing. Adapt the endpoint and protocol to the equipment.
FIFO requests that fail after transmission terminate their session; create a new connection.
Reconnection restores connections/subscriptions, not failed writes or missing history.
Bounded MQTT/OPC UA queues may drop new values when full; monitor their drop counters.
The complete communication path is not allocation-free.

## OPC UA security

Use `new OpcUaClientBridge(new OpcUaOptions { EndpointUrl = "opc.tcp://server:4840" })` for
SignAndEncrypt / Basic256Sha256 and strict certificate validation. Configure certificate trust
on both sides and set PkiRootPath for the application certificate and trust stores.
UserName/Password requires SignAndEncrypt; UserCertificate requires a private key.
The older string constructor retains the legacy anonymous, unsecured connection path.

## Documentation and license

See the repository's `docs/ko/RELEASE_GUIDE.md` or `docs/en/RELEASE_GUIDE.md` for installation,
upgrade, security and release procedures, and `docs/ko/SOFTWARE_HARDENING_USAGE.md` for examples.
Documentation: [Kable](https://johnny-hw.github.io/Kable/).
Apache-2.0. A copy of LICENSE is included in this package.

## Multi-client server (1.10.0)

Use KableServerBuilder<TMessage> with a per-connection codec factory, then StartAsync.
SendAsync and BroadcastAsync acknowledge bounded queue acceptance. StopAsync drains outgoing
queues and aborts at its drain deadline; DisposeAsync awaits cleanup. Create a new server after
stopping. See [server hosting](https://github.com/Johnny-HW/Kable/blob/main/docs/en/SERVER_HOSTING.md).
