# Kable 설치·보안·업그레이드·배포 안내

상태: **1.9.2 NuGet 게시 완료 (2026-10-28)**. [공식 패키지](https://www.nuget.org/packages/Kable/1.9.2)와 [게시·공식 피드 검증 결과](../development/2026-10-28-nuget-publication/walkthrough.md)를 참고한다.
이 안내는 배포 전에 먼저 작성하며, 아래 검증 항목의 결과는
[릴리즈 준비 결과](../development/2026-10-28-release-readiness/walkthrough.md)에 기록한다.

## 환경과 패키지 선택

| 패키지 | 지원 목표 / 용도 |
| --- | --- |
| Kable.Core, Kable | netstandard2.0 / .NET 8 / .NET 10, 기본 TCP·Serial·Pipe·TLS·세션 |
| Kable.Modbus | .NET 8 / .NET 10, Modbus TCP/RTU·PLC 태그·다중 장비 |
| Kable.Mqtt | .NET 8 / .NET 10, 발행·구독·재구독 |
| Kable.OpcUa | .NET 8 / .NET 10, 보안 세션·노드 RW·구독·복원 |
| Kable.Generators, Kable.ProtocolPack | netstandard2.0, 명령 생성·YAML 정의 |
| Kable.UI.Wpf | net8.0-windows / net10.0-windows, Windows UI |
| Kable.SharedMemory | net10.0-windows, Windows 공유 메모리 |
| Kable.Grpc, Kable.Transport.Ipc | netstandard2.0 / .NET 8 / .NET 10 |
| Kable.Engine.Disruptor, Kable.Observability.OpenTelemetry | .NET 8 / .NET 10 |
| Kable.Melsec | .NET 10, MELSEC SLMP |
| Kable.Host | .NET 10 실행 프로그램. NuGet 제품 패키지로 배포하지 않는다. |

실제 패키지의 lib/ 및 nuspec 일치 여부를 pack 단계에서 검증한다.
Serial과 공유 메모리의 플랫폼 제약은 사용하는 드라이버와 구현을 따른다.

## 설치와 시작

게시된 버전을 사용하는 일반 앱:

```powershell
dotnet new console -n EquipmentApp -f net8.0
Set-Location EquipmentApp
dotnet add package Kable --version 1.9.2
```

미게시 후보 버전은 위 명령으로 설치된다고 가정하지 않는다.
검증 중에는 로컬 feed를 사용하는 package-consumer 스크립트로 설치한다.
필요한 Modbus/Mqtt/OpcUa 패키지만 추가하고 Kable 제품 패키지의 버전을 맞춘다.

```csharp
using Kable.Simple;
await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
Console.WriteLine(await device.QueryAsync("STATUS?"));
```

기본 문자열 통신은 ASCII/LF다. 주소·포트·구분자는 장비 사양에 맞춘다.
장비 없이 시작하려면 samples/Kable.QuickStart를 실행한다.
[Modbus 사용법](MODBUS_USAGE.md), [장비 관리·태그·TLS·구독](SOFTWARE_HARDENING_USAGE.md)을 이어 읽는다.

## OPC UA 보안 설정

새 Options 생성자는 암호화 및 서버 인증서 검증을 기본으로 사용한다.
명시한 보안 모드/정책을 제공하는 엔드포인트가 없으면 실패하며 비보안으로 낮추지 않는다.

```csharp
using Kable.OpcUa;
using Opc.Ua;

var options = new OpcUaOptions
{
    EndpointUrl = "opc.tcp://plc.example.com:4840",
    SecurityMode = MessageSecurityMode.SignAndEncrypt,
    SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
    AutoAcceptUntrustedCertificates = false,
    PkiRootPath = @"C:\EquipmentApp\pki"
};
await using var bridge = new OpcUaClientBridge(options);
await bridge.ConnectAsync();
```

PKI는 own(개인 키 포함 앱 인증서), trusted(신뢰 서버), issuer(발급자), rejected(거부 인증서)로 나눈다.
서버 인증서를 thumbprint와 호스트명으로 확인한 후 trusted/certs에 설치하고,
서버에도 클라이언트 앱 인증서를 신뢰하도록 등록한다. 개인 키·암호·토큰을 저장소나 로그에 넣지 않는다.
사용자명/암호는 UserName/Password, 사용자 인증서는 UserCertificate로 설정한다.
암호를 쓰는 인증은 SignAndEncrypt에서만 허용한다. 사용자 인증과 앱 인증서는 별개다.

AutoAcceptUntrustedCertificates는 개발용이다. 켜더라도 만료·호스트명 오류까지 허용하지 않는다.
기존 문자열 생성자는 과거의 익명·비보안 연결과의 호환 경로다. 새 앱에서는 Options를 사용한다.
개발용 비보안 서버가 꼭 필요하면 SecurityMode=None, SecurityPolicyUri=None를 명시한다.

## 1.9.2 업그레이드 확인

- Options/DI를 사용하는 OPC UA 연결은 보안 기본값이 강화된다. 서버 정책과 신뢰 저장소를 설정한다.
- 문자열 생성자는 호환 동작을 유지하지만 새 보안 Options로 전환하는 것이 좋다.
- .NET 8 지원은 빌드뿐 아니라 실제 .NET 8 테스트와 소비자 실행 결과를 확인한다.
- IModbusMaster·IOpcUaClientBridge 기존 인터페이스는 유지한다. 새 API는 확장 계약/구체 타입으로 사용한다.
- FIFO 송신 후 실패는 세션을 종료하며 쓰기를 자동 재전송하지 않는다.
- 알람은 IsAlarmMessage 또는 선택적 코덱 IAlarmMessageClassifier 계약으로 분류한다. 기존 FAULT/EMERGENCY 부분 문자열 의존 코드는 명시적 프로토콜 규칙으로 옮긴다.
- Wait 텔레메트리는 별도 유한 전달 큐를 사용한다. InboundDeliveryCapacity까지 포화되면 TelemetryBufferOverflowException으로 종료한다. [설정 예제](DEVELOPER_USAGE.md#알람-분류와-느린-스트림-소비자-192)를 확인한다.
- TCP/NamedPipe와 MQTT/OPC UA는 정리 오류를 AggregateException으로 보고할 수 있다. DisposeAsync도 await하고 오류를 기록한다.
- 유한 구독 큐의 드롭 정책과 재연결 중 누락 가능성을 앱에서 처리한다.
- 전체 통신은 무할당이 아니다. 실제 할당/성능 기준은 기존 측정 범위를 따른다.

## 배포 순서

1. 이 안내·보안 예제·릴리즈 노트·CHANGELOG·지원 TFM을 최종 코드와 맞춘다.
2. 전체 솔루션 빌드, 테스트, 샘플과 UI/Host smoke를 확인한다.
3. 제품 패키지 전부를 로컬 pack하고 README·라이선스·종속성과 Analyzer 동봉을 검사한다.
4. 격리 캐시의 빈 소비자 프로젝트에서 로컬 패키지를 설치하고 .NET 8/10 및 WPF 예제를 실행한다.
5. SHA-256과 검증 결과를 기록하고 게시할 패키지 목록·버전을 검토한다.
6. 최종 게시 승인 후 NuGet 업로드와 태그/릴리즈를 수행한다. 재게시할 수 없는 버전은 덮어쓰지 않는다.

1.9.2는 아래 검증과 게시를 완료했다. 이후 버전도 같은 순서로 검증한 뒤 게시한다.

재현 도구:

```powershell
./tools/release/VerifySolution.ps1
./tools/release/VerifyPackages.ps1
```

패키지 검증은 실행별 artifacts/release-readiness/ 아래에 feed, 격리 cache, 소비자 코드와
SHA-256이 포함된 packages.json을 남긴다. 제품 패키지는 이 feed만 사용하고,
외부 종속성은 읽기 전용 기존 캐시 feed 또는 nuget.org에서 복원한다.
전체 테스트 TRX는 artifacts/release-readiness/solution-tests/에 저장한다.

## 서버 호스팅 (1.10.0)

[서버 가이드](SERVER_HOSTING.md)와 [릴리즈 노트](../releases/1.10.0.ko.md)를 참고한다.
