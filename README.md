# 🔌 Kable

> **High-Performance, Zero-Allocation Reactive Hardware Communication Engine for .NET**  
> Combining Microsoft Bedrock's `System.IO.Pipelines` transport abstraction with RSocket interaction patterns, ready-made WPF terminal diagnostics, and industrial-grade multi-language localization.

[![Language](https://img.shields.io/badge/Language-C%23%2014-blue.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![Targets](https://img.shields.io/badge/Targets-.NET%2010%20%7C%20.NET%208%20%7C%20netstandard2.0-purple.svg)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/Docs-GitHub%20Pages-blue.svg)](https://Johnny-HW.github.io/Kable/)
[![License](https://img.shields.io/badge/License-Apache%202.0-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-178%20Passing-brightgreen.svg)]()

---

## 📖 Live Web Documentation (Docsify)
👉 **[Browse Interactive Documentation Portal (7 Languages)](https://Johnny-HW.github.io/Kable/)**

---

## 🌐 Documentation Languages

- [English (Current)](README.md)
- [한국어 (Korean)](README.ko.md)
- [Web Documentation Portal (All Languages)](https://Johnny-HW.github.io/Kable/)

---

## ✨ Key Features

- **Pure Multi-Targeting**: Native support for `.NET 10.0`, `.NET 8.0 (LTS)`, and `netstandard2.0` (.NET Framework 4.8 / Legacy systems).
- **Zero-Copy Pipelines & Zero-GC Budget**: Vector-accelerated buffer parsing via `System.IO.Pipelines` and `ReadOnlySequence<byte>` (<1KB allocation budget per transaction).
- **Industrial Multi-Language Localization (7 Languages)**:
  - English (`en-US`), Korean (`ko-KR`), Chinese Simplified (`zh-CN`), Chinese Traditional (`zh-TW`), Japanese (`ja-JP`), German (`de-DE`), French (`fr-FR`).
  - Runtime culture switching via `KableLocalizer.Instance.SetCulture(...)` with auto-translated error messages for equipment operators.
- **Ready-Made WPF Terminal (`Kable.UI.Wpf`)**:
  - Drag-and-drop `<kable:CommTerminalView />` XAML UserControl with live packet inspection, Wireshark-style hex dump, and manual command injection.
- **Offline Mock Simulator (`UseSimulator`)**:
  - Full in-memory zero-network cross-piped loopback simulator enabling sequence logic development before physical hardware delivery.
- **Standard Wireshark PCAP Capture & Packet Replay**:
  - Export live packet sessions directly to `.pcap` files and replay captured field anomalies with speed multipliers (`PacketReplayer`).
- **Real-Time Jitter & RTT Watchdog (`SessionHealthMonitor`)**:
  - Circular buffer moving average & standard deviation (Jitter) calculation with automatic SECS-GEM / MES warning alarm dispatch.
- **Alarm Lifecycle Manager (`AlarmManager`)**:
  - Semiconductor 4-tier severity (`Info`, `Warning`, `Critical`, `Fatal`) and state tracking (`Set`/`Clear`) with lock-free `ChannelReader` streaming.
- **Analog Deadband Filter (`DeadbandFilter<T>`)**:
  - Thread-safe jitter noise suppressor preventing unnecessary high-frequency reporting to host MES.

---

## 🚀 Quick Start

### 1. Configuration-Driven Session (`KableDeviceOptions`)

```csharp
using Kable.Extensions;
using Kable.Configuration;
using Kable.Codecs;

// Inject options mapped from JSON, INI, YAML, TOML, or DB:
var options = new KableDeviceOptions
{
    DeviceId = "WAFER_ROBOT",
    Transport = "Serial",   // "Tcp", "Serial", "NamedPipe", "Simulator"
    PortName = "COM3",
    BaudRate = 115200,
    TimeoutMs = 3000
};

await using var session = new KableClientBuilder<string>()
    .UseOptions(options)
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
    .Build();

await session.StartAsync();
```

### 2. Fluent Builder with Offline Simulator

```csharp
using Kable.Extensions;
using Kable.Codecs;

// Develop against an offline mock simulator in 5 lines:
await using var session = new KableClientBuilder<string>()
    .UseSimulator(sim =>
    {
        sim.OnCommand("STATUS", "STATUS:READY")
           .OnCommand("GET_TEMP", "TEMP:24.5");
    })
    // For real hardware:
    // .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 9600)
    // .UseNamedPipe("local_hardware_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
    .WithDeviceId("ROBOT_A")
    .Build();

await session.StartAsync();

string status = await session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(2));
Console.WriteLine(status); // "STATUS:READY"
```

### 2. Ready-Made WPF UI Terminal (`Kable.UI.Wpf`)

Add real-time hardware communication terminal to your WPF application in XAML:

```xml
<Window x:Class="MyEquipmentApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:kable="clr-namespace:Kable.UI.Wpf;assembly=Kable.UI.Wpf">
    <Grid>
        <!-- Ready-made diagnostic terminal with live list, hex dump, and manual send -->
        <kable:CommTerminalView x:Name="CommTerminal" />
    </Grid>
</Window>
```

Code-behind:
```csharp
var vm = new CommTerminalViewModel();
CommTerminal.DataContext = vm;

// Hook up manual injection
vm.ManualSendRequested += async (cmd) => await session.SendAsync(cmd);

// Hook session observer to VM
builder.WithObserver(vm);
```

### 3. Industrial Localization (i18n)

```csharp
using System.Globalization;
using Kable.Localization;

// Switch runtime language to Japanese or German
KableLocalizer.Instance.SetCulture(new CultureInfo("ja-JP"));

try
{
    await session.RequestAsync<string>("MOVE_AXIS", TimeSpan.FromSeconds(3));
}
catch (DeviceTimeoutException ex)
{
    // Returns translated localized message: "コマンド 'MOVE_AXIS' の応答が 3000ms 待機後にタイムアウトしました。"
    MessageBox.Show(ex.GetLocalizedMessage());
}
```

### 4. Wireshark PCAP Dump & Anomaly Replay

```csharp
using Kable.Observability;

// 1. Dump session to Wireshark .pcap
var pcapObserver = new PcapStreamObserver("dump.pcap");
builder.WithObserver(pcapObserver);

// 2. Replay captured packets at double speed in lab
var replayer = new PacketReplayer(capturedRecords).WithSpeed(2.0);
await replayer.ReplayAsync(async packet =>
{
    await simulatedSession.ProcessPacketAsync(packet);
});
```

### 5. Type-Safe Device Profile Manager (`KableProfileManager`)

```csharp
using Kable.Engine.Profiles;

public enum RobotTelemetry { Status, ArmPosition, VacuumPressure }
public enum RobotControl   { ServoOn, MoveHome, PickWafer }

var config = new KableProfileConfig<RobotTelemetry, RobotControl>
{
    Host = "192.168.0.100",
    Port = 9000
};

config.AddPeriodic(RobotTelemetry.Status, "?STATUS", TimeSpan.FromMilliseconds(100));
config.AddAperiodic(RobotControl.ServoOn, "CMD:SERVO=1");

await using var client = await KableProfileManager.ConnectAsync(config);
string result = await client.ExecuteAsync(RobotControl.ServoOn);
```

---

## 📄 License & Governance

- **Core Engine & Framework**: [Apache License 2.0](LICENSE)
- **Third-Party Open-Source Notices**: [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
- **Zero-Copyleft Guarantee**: Fully permissive for 100% closed-source commercial equipment control binary distribution.
