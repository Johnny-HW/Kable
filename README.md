# 🔌 Kable

> **High-Performance, Zero-Allocation Reactive Hardware Communication Engine for .NET**  
> Combining Microsoft Bedrock's `System.IO.Pipelines` transport abstraction with RSocket interaction patterns.

---

## ✨ Key Features

- **Pure Multi-Targeting**: Native support for `.NET 10.0`, `.NET 8.0 (LTS)`, and `netstandard2.0` (.NET Framework 4.8 / Legacy systems).
- **0-GC Pipelines I/O**: Zero memory copies and vector-accelerated buffer parsing via `System.IO.Pipelines` and `ReadOnlySequence<byte>`.
- **Hybrid Transaction Router**:
  - **No-Correlation ID Devices** (RS-232C, Simple ASCII): Automatic asynchronous preemptive FIFO lock (`SemaphoreSlim`) preventing request interleaving.
  - **Correlation ID Protocols** (Modern TCP/IPC): High-speed lock-free pipelining & interleaving.
- **Fail-Fast Safety Policy**: Immediate `DeviceDisconnectedException` dispatch upon cable/link disconnection to guarantee physical hardware safe-state.
- **Tri-Stream Observability**: Independent bounded ringbuffers (`DropOldest`) separating Periodic Telemetry, Command Console, and Spontaneous Alarms to prevent UI lagging.

---

## 🚀 Quick Start

### 1. Fluent Builder (`KableClientBuilder`)

```csharp
using Kable.Extensions;
using Kable.Codecs;

// Connect via TCP, Serial Port, or NamedPipe in 3 lines:
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 9600)
    // .UseNamedPipe("local_hardware_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
    .Build();

await session.StartAsync();

// Request-Response with Fail-Fast Watchdog
string response = await session.RequestAsync<string>("START_ACQUISITION", TimeSpan.FromSeconds(3));

// Subscribe to Real-time Stream
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"Received telemetry: {packet}");
}
```

### 2. Dependency Injection (`Microsoft.Extensions.DependencyInjection`)

```csharp
builder.Services.AddKable(); // Registers ICommObserver (3-channel ringbuffer)

builder.Services.AddKableSession<string>((client, sp) =>
{
    client.UseSerialPort("COM3", baudRate: 9600)
          .UseCodec(new AsciiLineCodec(delimiter: 0x0D));
});
```

### 3. Lightweight Simple Facade (`KableSimple`)

For quick prototypes and minimal ceremony (always use `await using` to ensure non-blocking cleanup):

```csharp
using Kable.Simple;

// Open connection and guarantee clean non-blocking disposal
await using var client = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);

// Subscribe to real-time events & errors
client.LineReceived += line => Console.WriteLine($"Rx: {line}");
client.ErrorOccurred += ex => Console.Error.WriteLine($"Error: {ex.Message}");

// Send single line or Query (Request-Response)
await client.SendLineAsync("SET:PARAM=1");
string reply = await client.QueryAsync("GET:PARAM?");
```

---

## 📄 License & Governance

- **Core Engine & Framework**: [Apache License 2.0](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/LICENSE)
- **Third-Party Open-Source Notices**: [THIRD_PARTY_LICENSES.md](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/THIRD_PARTY_LICENSES.md)
- **Detailed Compliance Guide**: [07. Open-Source Licensing & Compliance Guide](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/docs/07_OPENSOURCE_LICENSING_AND_COMPLIANCE.md)
- **Zero-Copyleft Guarantee**: 장비 제어 시퀀스 및 독점 레시피 알고리즘의 소스코드를 외부에 공개할 필요가 전혀 없으며, 100% 비공개 상용 바이너리로 안전하게 납품 가능합니다.

