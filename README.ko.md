# 🔌 Kable (한국어 개발자 가이드)

> **.NET 기반 초고성능 Zero-Allocation 반응형 하드웨어 통신 프레임워크**  
> 마이크로소프트 Bedrock의 `System.IO.Pipelines` 파이프라인 추상화와 RSocket 인터랙션 패턴, 레디메이드 WPF 통신 모니터링 컨트롤, 산업용 7개 국어 다국어 지원 엔진을 결합한 장비 제어 통신 솔루션입니다.

[![Language](https://img.shields.io/badge/언어-C%23%2014-blue.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![Targets](https://img.shields.io/badge/타깃-.NET%2010%20%7C%20.NET%208%20%7C%20netstandard2.0-purple.svg)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/문서-GitHub%20Pages-blue.svg)](https://Johnny-HW.github.io/Kable/)
[![License](https://img.shields.io/badge/라이선스-Apache%202.0-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/단위테스트-267개%20통과-brightgreen.svg)]()

---

## 📖 실시간 웹 기술 문서 포털 (Docsify)
👉 **[Kable 대화형 웹 기술 문서 사이트 바로가기 (7개 국어 지원)](https://Johnny-HW.github.io/Kable/)**

---

## 🌐 문서 언어 선택

- [English (글로벌 표준 영문)](README.md)
- [한국어 (현재 문서)](README.ko.md)
- [웹 문서 포털 (7개 언어 지원)](https://Johnny-HW.github.io/Kable/)

---

## ✨ 핵심 아키텍처 및 주요 기능

1. **순수 멀티 타기팅 (Multi-Targeting)**
   - 최신 `.NET 10.0`, LTS 버전인 `.NET 8.0`, 레거시 설비 및 .NET Framework 4.8 연동을 위한 `netstandard2.0`을 100% 네이티브 지원합니다.
2. **Zero-Copy & Zero-GC 버퍼 파이프라인**
   - 메모리 복사 없는 `System.IO.Pipelines` 및 `ReadOnlySequence<byte>` 기반의 벡터 가속 버퍼 파싱을 통해 트랜잭션당 1KB 미만의 극저할당(Zero-GC Budget)을 달성했습니다.
3. **산업용 멀티 프로토콜 어댑터 스위트**
   - **Modbus-TCP** (`Kable.Modbus`), **미쓰비시 SLMP/MC 프로토콜 3E** (`Kable.Melsec`), **OPC UA 클라이언트** (`Kable.OpcUa`), **MQTT 텔레메트리** (`Kable.Mqtt`), **gRPC 양방향 스트리밍** (`Kable.Grpc`).
4. **초저지연 공유 메모리 IPC (`Kable.SharedMemory`)**
   - Windows `MemoryMappedFile` 기반 락프리 링버퍼로 서브밀리초 초고속 IPC 및 아날로그 센서 파형(Waveform) 스트리밍을 지원합니다.
5. **초간단 3줄 비동기 파사드 (`KableSimple`)**
   - 복잡한 세션/빌더 구성 없이 단 3줄 코드로 직렬, TCP, NamedPipe 통신 세션을 가동할 수 있는 Zero-Boilerplate 고수준 API를 제공합니다.
6. **글로벌 반도체/FA 7개 언어 현지화 (i18n)**
   - **한국어(`ko-KR`)**, **영어(`en-US`)**, **중국어 간체(`zh-CN`)**, **중국어 번체(`zh-TW`)**, **일본어(`ja-JP`)**, **독일어(`de-DE`)**, **프랑스어(`fr-FR`)** 내장.
   - 런타임 언어 즉시 전환(`SetCulture`) 및 설비 오퍼레이터 화면용 예외 메시지 자동 번역 지원.
7. **WPF 전용 레디메이드 통신 터미널 (`Kable.UI.Wpf`)**
   - XAML 태그 한 줄(`<kable:CommTerminalView />`)로 송수신 패킷 모니터링, 와이어샤크 스타일 16진수 헥사 덤프, 수동 커맨드 즉시 전송(Manual Injection) UI를 제공합니다.
8. **오프라인 모의 시뮬레이터 (`UseSimulator`)**
   - 실물 하드웨어(로봇, 얼라이너, PLC)가 아직 입고되지 않은 개발 초기에도 인메모리 루프백 시뮬레이터로 상위 시퀀스를 즉시 개발할 수 있습니다.
9. **Wireshark 표준 PCAP 덤프 및 패킷 리플레이어 (`PacketReplayer`)**
   - 통신 세션을 표준 `.pcap` 파일로 자동 기록하고, 현장 라인(Fab/Site)에서 발생한 간헐적 통신 장애를 개발 PC 및 오프라인 테스트 환경에서 타임스탬프 기반 배속 재생으로 재현합니다.
10. **산업용 신뢰성 하든 및 알람 스풀 (`AlarmOverflowMode`, `AlarmSpooler`)**
    - 30종 극한 신뢰성 테스트(`TC_REL_01`~`30`)를 전수 통과한 라이프사이클 보호, 단일 클린업 수명주기, 고빈도 알람 폭주 시 세션 안전 격리 및 비동기 드레인을 보장합니다.

---

## 🚀 빠른 시작 가이드 (Quick Start)

> 💡 **로컬 모의 장비 기반 10분 온보딩 샘플:**  
> 저장소 내 [samples/Kable.QuickStart](samples/Kable.QuickStart) 프로젝트를 실행하면 별도 하드웨어 없이 즉시 로컬 TCP 모의 장비를 띄우고 `KableSimple` 연결, 질의응답, 자발 이벤트 수신을 검증할 수 있습니다.
> ```powershell
> dotnet run --project samples/Kable.QuickStart/Kable.QuickStart.csproj
> ```
> 세션 수명 주기 및 타임아웃/단절 시 안전한 복구 패턴은 **[세션 수명 주기 및 장애 복구 가이드](docs/ko/CONNECTION_LIFECYCLE.md)**를 참조하십시오.

---

## 🛠️ 실전 코드 예제

### 1. 설정 모델(Config / Options)로 시작하기 (INI, JSON, YAML 등 연동)

```csharp
using Kable.Extensions;
using Kable.Configuration;
using Kable.Codecs;

// 상위 앱이 JSON, INI, YAML, TOML 등에서 읽어온 설정을 POCO 레코드로 주입:
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

// 장비로 요청 전송 및 응답 수신:
string status = await session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(2));
Console.WriteLine($"Robot Status: {status}");
```

### 2. 오프라인 시뮬레이터로 시작하기 (실물 하드웨어 불필요)

```csharp
using Kable.Extensions;
using Kable.Codecs;

// 하드웨어 없이 5줄로 통신 세션 구축:
await using var session = new KableClientBuilder<string>()
    .UseSimulator(sim =>
    {
        sim.WithLatency(TimeSpan.FromMilliseconds(50)) // 기구 응답 지연 모사
           .OnCommand("STATUS", "STATUS:READY")
           .OnCommand("GET_TEMP", "TEMP:24.5");
    })
    // 실물 장비 연결 시 한 줄로 전환:
    // .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 9600)
    // .UseNamedPipe("local_hardware_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A))
    .WithDeviceId("ROBOT_A")
    .Build();

await session.StartAsync();

string status = await session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(2));
Console.WriteLine(status); // "STATUS:READY" 출력
```

### 3. 초간단 3줄 비동기 파사드 (`KableSimple`)

```csharp
using Kable.Simple;

// 복잡한 빌더 없이 3줄로 TCP 소켓 연결 및 질의응답
await using var client = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
string idn = await client.QueryAsync("*IDN?");
Console.WriteLine($"Connected Device: {idn}");
```

### 4. WPF 화면에 통신 터미널 UserControl 연결하기 (`Kable.UI.Wpf`)

#### XAML
```xml
<Window x:Class="MyEquipmentApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:kable="clr-namespace:Kable.UI.Wpf;assembly=Kable.UI.Wpf">
    <Grid>
        <!-- 실시간 송수신 목록, 헥사 덤프, 필터, 수동 전송창이 내장된 컨트롤 -->
        <kable:CommTerminalView x:Name="TerminalView" />
    </Grid>
</Window>
```

#### C# 비하인드 코드
```csharp
var terminalVm = new CommTerminalViewModel();
TerminalView.DataContext = terminalVm;

// 수동 전송 버튼 클릭 시 세션으로 발송
terminalVm.ManualSendRequested += async (cmd) => await session.SendAsync(cmd);

// 세션 옵저버에 바인딩
builder.WithObserver(terminalVm);
```

### 5. 글로벌 7개 국어 다국어 예외 처리

```csharp
using System.Globalization;
using Kable.Localization;

// 일본 현지 팹 납품 시 일본어로 즉시 변경
KableLocalizer.Instance.SetCulture(new CultureInfo("ja-JP"));

try
{
    await session.RequestAsync<string>("MOVE_AXIS", TimeSpan.FromSeconds(3));
}
catch (DeviceTimeoutException ex)
{
    // 일본어로 자동 번역된 메시지 출력:
    // "コマンド 'MOVE_AXIS' の応答が 3000ms 待機後にタイムアウトしました。"
    MessageBox.Show(ex.GetLocalizedMessage());
}
```

### 6. 와이어샤크 PCAP 덤프 및 패킷 리플레이 (장애 재현)

```csharp
using Kable.Observability;

// 1. 현장에서 통신 패킷을 Wireshark 표준 .pcap 파일로 기록
var pcapObserver = new PcapStreamObserver("equipment_trace.pcap");
builder.WithObserver(pcapObserver);

// 2. 캡처된 패킷 로그를 개발 PC 환경에서 2배속으로 리플레이하여 간헐적 장애 재현
var replayer = new PacketReplayer(capturedList).WithSpeed(2.0);
await replayer.ReplayAsync(async record =>
{
    Console.WriteLine($"[{record.TimestampUtc:O}] {record.Direction} {record.Tag}: {record.PayloadString}");
    // 모의 장비 채널이나 파이프라인으로 패킷 바이트 주입
    await mockChannel.Writer.WriteAsync(record.Payload);
});
```

### 7. Enum 기반 디바이스 프로필 관리 (`KableProfileManager`)

```csharp
using Kable.Engine.Profiles;

// 장비 제어 커맨드 정의 (Enum 표준)
public enum RobotTelemetry { Status, ArmPosition, VacuumPressure }
public enum RobotControl   { ServoOn, MoveHome, PickWafer }

var config = new KableProfileConfig<RobotTelemetry, RobotControl>
{
    Host = "192.168.0.100",
    Port = 9000
};

// 100ms 주기 배경 폴링 등록
config.AddPeriodic(RobotTelemetry.Status, "?STATUS", TimeSpan.FromMilliseconds(100));
config.AddAperiodic(RobotControl.ServoOn, "CMD:SERVO=1");

await using var client = await KableProfileManager.ConnectAsync(config);

// 배경 폴링과 스레드 경합 없이 안전하게 서보 온 실행
string result = await client.ExecuteAsync(RobotControl.ServoOn);
```

### 8. 산업용 프로토콜 및 고속 공유메모리 확장 (`Kable.Modbus`, `Kable.SharedMemory`)

```csharp
using Kable.SharedMemory.Memory;

// 초고속 서브밀리초 MMF IPC 링버퍼 (동일 머신 프로세스 간 통신)
using var serverBuffer = SharedMemoryRingBuffer.Create("wafer_aligner_channel", 65536);
using var clientBuffer = SharedMemoryRingBuffer.Open("wafer_aligner_channel");

byte[] payload = System.Text.Encoding.UTF8.GetBytes("HIGH_SPEED_STREAM_DATA");
serverBuffer.Write(payload);
```

---

## 📄 라이선스 및 상업적 배포

- **프레임워크 라이선스**: [Apache License 2.0](LICENSE)
- **오픈소스 고지서**: [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
- **Zero-Copyleft 보장**: 고객사의 고유한 장비 제어 시퀀스나 레시피 알고리즘을 공개할 의무가 일절 없으며, 100% 클로즈드 소스 상업용 장비 제어 소프트웨어에 완전히 자유롭게 번들링하여 배포할 수 있습니다.
