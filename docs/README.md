<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 INDUSTRIAL HARDWARE COMMUNICATION ENGINE</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable Documentation
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      A high-performance, zero-allocation reactive hardware communication engine combining Microsoft Bedrock's <code>System.IO.Pipelines</code> transport abstraction with RSocket interaction patterns.
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC Pipelines I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast Safe-State</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Tri-Stream Observability</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines Zero-Copy</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Eliminates socket and serial memory buffer copying using <code>ReadOnlySequence&lt;byte&gt;</code> vector-accelerated framing with allocation budgets under 1KB/request.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 Hybrid Transaction Router</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Legacy ASCII/Serial instruments without correlation tokens are serialized via an asynchronous FIFO lock, while modern protocols execute lock-free multiplexing.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast Safety Policy</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Dispatches <code>DeviceDisconnectedException</code> immediately upon physical link drop without dangerous retries, transitioning hardware to an instant safe-state.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 Tri-Stream Observability</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Isolates periodic telemetry, command consoles, and spontaneous alarms into independent bounded ringbuffers (<code>DropOldest</code>) to prevent UI lag.
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 Developer Quick Start
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Step 1. Package Installation (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# Core communication engine (TCP, Serial, NamedPipe, Codecs, Builder)
dotnet add package Kable

# Pure abstraction contracts and interfaces only
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Step 2. Fluent Builder Connection (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Configure transport layer and protocol codec cleanly in 3 lines:
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. Build session instance
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // Delimited by LF
    .Build();

// 2. Start asynchronous I/O pipeline
await session.StartAsync();

// 3. Request-Response RPC with 3-second watchdog deadline
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"Temperature response: {response}");

// 4. Ingest real-time asynchronous telemetry stream
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"Telemetry packet: {packet}");
}
```

</div>
</div>

<!-- Step 3: KableSimple Facade -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Step 3. Lightweight Facade (`KableSimple`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Minimal ceremony for quick diagnostics, hardware prototypes, and sequence tests:
</p>

```csharp
using Kable.Simple;

// Open connection and guarantee non-blocking disposal
await using var client = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);

// Subscribe to real-time events & errors
client.LineReceived += line => Console.WriteLine($"[Rx] {line}");
client.ErrorOccurred += ex => Console.Error.WriteLine($"[Error] {ex.Message}");

// Send command or query
await client.SendLineAsync("SERVO:ENABLE");
string status = await client.QueryAsync("SERVO:STATUS?");
```

</div>
</div>

<!-- Step 4: DI Container -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 30px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Step 4. Dependency Injection (`Microsoft.Extensions.DependencyInjection`)
</div>
<div style="padding: 14px 18px;">

```csharp
// 1. Register observability and multi-channel ringbuffers
builder.Services.AddKable();

// 2. Register typed device sessions
builder.Services.AddKableSession<string>((client, sp) =>
{
    client.UseSerialPort("COM3", baudRate: 9600)
          .UseCodec(new AsciiLineCodec(delimiter: 0x0D));
});
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 Technical Documentation Sitemap
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Document</th>
<th style="padding: 12px 16px; font-weight: 700;">Core Specification & Scope</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">Master documentation index and 4 confirmed architectural decisions</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. Architecture Overview</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines transport layer, RSocket interaction model, and class diagrams</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. Core Interfaces Spec</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, and <code>IConnectionContext</code> contracts</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. Observability & Logging</a></td>
<td style="padding: 12px 16px; color: #475569;">Tri-stream bounded ringbuffers (<code>DropOldest</code>) and 60 FPS UI responsiveness</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. Implementation Layout</a></td>
<td style="padding: 12px 16px; color: #475569;">Repository directory hierarchy, namespace taxonomy, and NuGet packaging</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. Industrial Checksums</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-allocation CRC-16 (Modbus/CCITT), LRC, and XOR BCC look-up tables</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. Industrial Comm Roadmap</a></td>
<td style="padding: 12px 16px; color: #475569;">13 industrial communication protocols spectrum, determinism classes, and roadmap</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. Open-Source Licensing</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft guarantee, permissive license matrix (Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>
