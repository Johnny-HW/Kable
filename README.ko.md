# Kable

.NET 8, .NET 10, netstandard2.0용 비동기 장비 통신 라이브러리.
장비에 맞는 API를 선택해 연결·질의·주기 조회·연결 복구를 구성한다.

다음 배포 버전은 **1.9.2 (배포 예정)**이다. [릴리즈 노트와 업그레이드 안내](docs/releases/1.9.2.ko.md),
[변경 이력](CHANGELOG.md)을 참고한다. 아래 설치 명령은 NuGet에 게시된 버전을 설치한다.

## 한 번의 질의부터 시작하기

기존 .NET 앱의 프로젝트 폴더에서 패키지를 추가한다. 새 앱이라면 먼저 `dotnet new console -n KableDemo`와 `cd KableDemo`를 실행한다.

```bash
dotnet add package Kable
```

```csharp
using Kable.Simple;

await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
string status = await device.QueryAsync("STATUS?");
Console.WriteLine(status);
```

주소·포트·명령을 장비 사양에 맞게 바꾼다. 기본값은 ASCII와 LF 줄 구분자다.
OpenTcpAsync는 이미 시작된 클라이언트를 반환하므로 일반 통신을 위해
Thread, Task.Run 또는 별도 StartAsync를 만들 필요 없다. await using으로 비동기 정리한다.

장비가 없다면 저장소의 로컬 모의 장비 예제로 시작한다.

```bash
dotnet run --project samples/Kable.QuickStart/Kable.QuickStart.csproj
```

## 어떤 API를 선택할까?

| 하려는 작업 | 시작점 |
| --- | --- |
| 문자열 명령과 한 줄 응답 | `KableSimple` |
| 주기 조회와 최신값 캐시 | `KableProfileManager` |
| 바이너리/장비 전용 프로토콜 | `KableClientBuilder<T>`와 코덱 |
| Modbus / MELSEC 장비 | `Kable.Modbus` / `Kable.Melsec` 클라이언트 |
| 단절 후 선택적 연결 복구 | `ReconnectingKableClient` |

Builder는 Build 후 StartAsync가 필요하고, Simple Open/Profile Connect는 시작된 클라이언트를 반환한다.
내부 비동기 루프가 송신·수신·응답 매칭을 담당하며 각 루프가 전용 OS 스레드를 점유하지 않는다.
앱 고유의 반복 작업을 만들 때만 해당 Task를 보관하고 종료 시 취소 후 await한다.

## 이 순서로 읽기

1. [처음 연결하는 순서](docs/ko/GETTING_STARTED.md): 전송 선택, Task, Profile.
2. [설정과 이벤트 계약](docs/ko/DEVELOPER_USAGE.md): 연결/응답 제한시간, 이벤트 분류, UI 전환.
3. [실패 처리와 종료](docs/ko/ERROR_HANDLING.md): 예외별 다음 행동, 선택적 재연결, 정리.
4. [세션 수명 계약](docs/ko/CONNECTION_LIFECYCLE.md): FIFO/CID 차이와 세션 재사용.

## 먼저 알아둘 계약

- 연결 수립 제한시간과 개별 응답 제한시간은 서로 다른 설정이다.
- 질의 응답은 QueryAsync 반환값으로 받고, LineReceived는 소비되지 않은 라인/이벤트를 전달한다.
- QueryAsync의 오류는 await한 곳에서 처리한다. ErrorOccurred가 모든 오류를 대신 받지 않는다.
- FIFO 송신 후 타임아웃/취소가 발생하면 연결이 종료된다. 기존 클라이언트를 폐기하고 새로 연결한다.
- 재연결 래퍼는 새 연결로 복구하며 실패한 명령을 자동 재전송하지 않는다.
- 이동·쓰기 명령을 반복하기 전 실제 장비 상태를 확인한다.
- 앱 작업 취소·await, 이벤트 구독 해제 후 비동기로 클라이언트를 정리한다.

## 패키지와 확장 기능

| 패키지 | 용도 |
| --- | --- |
| `Kable` | Simple, Profile, Builder, TCP/Serial/Named Pipe, 코덱과 시뮬레이터 |
| `Kable.Core` | 추상 계약, 프로토콜 기반 타입과 공통 타입 |
| `Kable.Modbus`, `Kable.Melsec` | Modbus-TCP / MELSEC SLMP 클라이언트·코덱 |
| `Kable.OpcUa`, `Kable.Mqtt`, `Kable.Grpc` | 프로토콜 브리지와 스트리밍 통합 |
| `Kable.SharedMemory` | Windows 공유 메모리 전송과 파형 버퍼 |
| `Kable.UI.Wpf` | WPF 통신 터미널·진단 컨트롤 |
| `Kable.Observability.OpenTelemetry` | OpenTelemetry 연동 |

[Modbus TCP·RTU 및 PLC 값 변환 사용법](docs/ko/MODBUS_USAGE.md)에서 지원 기능과 예제를 확인한다.
[이름별 장비·PLC 태그·TLS·MQTT/OPC UA 구독](docs/ko/SOFTWARE_HARDENING_USAGE.md)의 사용법과 검증 범위도 참고한다.
[지원 환경·설치·보안·업그레이드·배포 안내](docs/ko/RELEASE_GUIDE.md)에서 패키지별 지원 환경과 배포 순서를 확인한다.

파이프라인 버퍼는 재사용하지만 문자열 수신과 비동기 질의는 메모리를 할당한다.
[성능 측정 범위](docs/ko/PERFORMANCE_BASELINE.md)를 기준으로 판단한다.

사용자 정의 프로토콜과 DI는 [아키텍처](docs/ko/01_ARCHITECTURE_OVERVIEW.md),
[핵심 인터페이스](docs/ko/02_CORE_INTERFACES.md), [관측성](docs/ko/03_OBSERVABILITY_LOGGING.md)으로 이어진다.

## 문서와 소스

- [English getting started](README.md)
- [GitHub Pages 문서](https://johnny-hw.github.io/Kable/)
- [컴파일되는 QuickStart 예제](https://github.com/Johnny-HW/Kable/tree/main/samples/Kable.QuickStart)
- [저장소](https://github.com/Johnny-HW/Kable) · [Apache-2.0 라이선스](LICENSE)
