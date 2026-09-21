# 🔌 Kable (한국어 개발자 가이드)

> **.NET 기반 초고성능 Zero-Allocation 반응형 하드웨어 통신 프레임워크**  
> 마이크로소프트 Bedrock의 `System.IO.Pipelines` 파이프라인 추상화와 RSocket 인터랙션 패턴, 레디메이드 WPF 통신 모니터링 컨트롤, 산업용 7개 국어 다국어 지원 엔진을 결합한 장비 제어 통신 솔루션입니다.

[![Language](https://img.shields.io/badge/언어-C%23%2014-blue.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![Targets](https://img.shields.io/badge/타깃-.NET%2010%20%7C%20.NET%208%20%7C%20netstandard2.0-purple.svg)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/문서-GitHub%20Pages-blue.svg)](https://Johnny-HW.github.io/Kable/)
[![License](https://img.shields.io/badge/라이선스-Apache%202.0-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/단위테스트-178개%20통과-brightgreen.svg)]()

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
3. **글로벌 반도체/FA 7개 언어 현지화 (i18n)**
   - **한국어(`ko-KR`)**, **영어(`en-US`)**, **중국어 간체(`zh-CN`)**, **중국어 번체(`zh-TW`)**, **일본어(`ja-JP`)**, **독일어(`de-DE`)**, **프랑스어(`fr-FR`)** 내장.
   - 런타임 언어 즉시 전환(`SetCulture`) 및 설비 오퍼레이터 화면용 예외 메시지 자동 번역 지원.
4. **WPF 전용 레디메이드 통신 터미널 (`Kable.UI.Wpf`)**
   - XAML 태그 한 줄(`<kable:CommTerminalView />`)로 송수신 패킷 모니터링, 와이어샤크 스타일 16진수 헥사 덤프, 수동 커맨드 즉시 전송(Manual Injection) UI를 제공합니다.
5. **오프라인 모의 시뮬레이터 (`UseSimulator`)**
   - 실물 하드웨어(로봇, 얼라이너, PLC)가 아직 입고되지 않은 개발 초기에도 인메모리 루프백 시뮬레이터로 상위 시퀀스를 즉시 개발할 수 있습니다.
6. **Wireshark 표준 PCAP 덤프 및 패킷 리플레이어 (`PacketReplayer`)**
   - 통신 세션을 표준 `.pcap` 파일로 자동 기록하고, 현장에서 발생한 간헐적 패킷 이상을 연구실에서 타임스탬프 기반 배속 재생으로 100% 재현합니다.
7. **실시간 RTT 및 지터 감시 워치독 (`SessionHealthMonitor`)**
   - 커맨드 왕복 시간(RTT) 및 지터(Jitter) 표준편차를 링버퍼로 실시간 연산하여, 통신 선로 불량이나 허브 병목 시 상위 MES/SECS-GEM 알람을 사전에 트리거합니다.
8. **반도체 표준 4단계 알람 생명주기 (`AlarmManager`)**
   - `Info`, `Warning`, `Critical`, `Fatal` 심각도 및 `Set`(발생)/`Clear`(해제) 전이 관리, 무손실 비동기 채널 스트림 제공.
9. **아날로그 센서 데드밴드 필터 (`DeadbandFilter<T>`)**
   - 센서 미세 지터로 인한 불필요한 상위 보고 트래픽을 임계치 기반으로 자동 억제합니다.

---

## 🚀 실전 코드 예제

### 1. 오프라인 시뮬레이터로 시작하기 (실물 하드웨어 불필요)

```csharp
using Kable.Extensions;
using Kable.Codecs;

// 하드웨어 없이 5줄로 통신 세션 구축:
await using var session = new KableClientBuilder<string>()
    .UseSimulator(sim =>
    {
        sim.OnCommand("STATUS", "STATUS:READY")
           .OnCommand("GET_TEMP", "TEMP:24.5");
    })
    // 실물 장비 연결 시:
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

### 2. WPF 화면에 통신 터미널 UserControl 연결하기

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

### 3. 글로벌 7개 국어 다국어 예외 처리

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

### 4. 와이어샤크 PCAP 덤프 및 패킷 리플레이 (장애 재현)

```csharp
using Kable.Observability;

// 1. 현장에서 통신 패킷을 Wireshark 표준 .pcap 파일로 기록
var pcapObserver = new PcapStreamObserver("equipment_trace.pcap");
builder.WithObserver(pcapObserver);

// 2. 연구실에서 수집된 패킷을 2배속으로 리플레이하여 간헐적 장애 재현
var replayer = new PacketReplayer(capturedList).WithSpeed(2.0);
await replayer.ReplayAsync(async packet =>
{
    await testSession.ProcessPacketAsync(packet);
});
```

### 5. Enum 기반 디바이스 프로필 관리 (`KableProfileManager`)

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

---

## 📄 라이선스 및 상업적 배포

- **프레임워크 라이선스**: [Apache License 2.0](LICENSE)
- **오픈소스 고지서**: [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
- **Zero-Copyleft 보장**: 고객사의 고유한 장비 제어 시퀀스나 레시피 알고리즘을 공개할 의무가 일절 없으며, 100% 클로즈드 소스 상업용 장비 제어 소프트웨어에 완전히 자유롭게 번들링하여 배포할 수 있습니다.
