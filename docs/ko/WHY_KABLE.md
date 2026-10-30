# 기본 .NET API로 장비 통신을 만들 때 Kable이 줄여주는 작업

Kable의 장점은 장비 연결, 요청·응답, 주기 조회, 이벤트, 복구의 공통 동작을
장비용 API와 수명 계약으로 제공한다는 점이다. 애플리케이션은 장비의 주소·명령·프로토콜과
업무 동작에 집중하고, 통신 기반 코드를 여러 프로젝트에서 반복 작성하는 부담을 줄인다.

이 자료는 기능과 구현 책임을 설명한다. 아래 구조는 기본 API로 동일한 요구사항을
구성할 때의 설계 예시이며, 기본 API의 기능 부족이나 필수 파일 개수를 주장하지 않는다.
개발 기간·코드 줄 수·결함 감소율은 별도로 측정하지 않아 수치로 제시하지 않는다.

## 1. 한 번 질의할 때의 코드

다음 예제는 .NET 8/10 콘솔 앱에서 ASCII·LF 장비에 한 번 질의한다.
주소와 명령은 장비 사양에 맞게 바꾼다. 정상 경로를 보여 주며 예외는 호출자에게 전파한다.

### 기본 .NET API

```csharp
using System.Net.Sockets;
using System.Text;

using var lifetime = new CancellationTokenSource();
using var client = new TcpClient { NoDelay = true };
await client.ConnectAsync("192.168.0.100", 9000, lifetime.Token);
await using var stream = client.GetStream();
using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
using var replyDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
replyDeadline.CancelAfter(TimeSpan.FromSeconds(2));

await stream.WriteAsync(Encoding.ASCII.GetBytes("STATUS?\n"), replyDeadline.Token);
string status = await reader.ReadLineAsync(replyDeadline.Token)
    ?? throw new EndOfStreamException("Device closed before replying.");
Console.WriteLine(status);
```

### Kable

`dotnet add package Kable`로 패키지를 추가한다.

```csharp
using Kable.Simple;

using var lifetime = new CancellationTokenSource();
await using var device = await KableSimple.OpenTcpAsync(
    "192.168.0.100", 9000, ct: lifetime.Token);
string status = await device.QueryAsync(
    "STATUS?", TimeSpan.FromSeconds(2), lifetime.Token);
Console.WriteLine(status);
```

Kable은 프레임 송수신과 응답 대기를 QueryAsync로 묶고 세션 수명을 관리한다.
기본 API 예제의 제한시간은 취소 토큰, Kable의 응답 제한시간은 요청 API의 timeout이다.
따라서 예외 형식과 세션 재사용 계약까지 동일한 예제는 아니다.
두 예제 모두 비동기로 동작하며, 일반 통신에 별도 Thread나 Task.Run이 필요하지 않다.

기본 API 예제는 응답 한 번을 읽고 연결을 정리한다. 이를 여러 명령·폴링·알람이
같은 연결을 공유하는 장비 클라이언트로 확장할 때 아래의 관리 작업이 생긴다.
StreamReader는 줄 단위 버퍼링과 분할 수신을 이미 처리한다. 직접 구성할 때도 이런 기본 타입을
활용할 수 있으며, 사용자 정의 프레임과 장비 요청 수명까지 연결하는 설계·검증 작업이 추가된다.

## 2. 요구사항이 늘어날 때 추가되는 구현 책임

| 장비 프로그램의 요구사항 | 기본 API 위에 구현·유지할 내용 | Kable이 제공하는 경로 | 앱이 정할 장비 규칙 |
| --- | --- | --- | --- |
| TCP·시리얼·Named Pipe 교체 | 전송별 연결·입출력·취소·정리와 공통 추상화 | Simple Open 메서드, IConnectionContext | 주소, 포트, 시리얼 설정 |
| 데이터가 나뉘거나 합쳐져 도착 | 부분 프레임 보관, 구분자/길이 파싱, 소비 위치, 최대 길이 | 코덱과 파이프라인 처리 | 프레임 구조·인코딩·길이 제한 |
| 여러 코드에서 같은 장비 조회 | FIFO 직렬화 또는 CID별 대기 등록·해제·응답 매칭 | RequestAsync·QueryAsync와 FIFO/CID 라우팅 | CID 지원·추출·재사용 규칙 |
| 조회 중 장비가 알람 전송 | 응답과 자발 메시지 분류, 각각의 전달 경로 | 코덱 분류, LineReceived·스트림 | 알람/응답 구분 기준 |
| 일정 간격 조회와 최신값 재사용 | 타이머, 작업 취소·종료, 명령별 캐시, 데이터 유효 기간 | Profile 주기 조회·캐시·TryGetFresh | 조회 명령·간격·허용 데이터 나이 |
| 요청 취소와 늦은 응답 | 송신 단계 추적, 대기 제거, 다음 요청의 오염 방지 정책 | FIFO 세션 수명 계약, CID 요청 정리 | 장비 실행 여부와 다음 업무 판단 |
| 단절 시 대기 중 호출 종료 | 연결 종료 감지, 대기자별 실패 전파, 중복 완료 방지 | 세션 종료와 요청 실패 전파 | 앱의 중단·복구 처리 |
| 단절 후 다시 연결 | 재시도 작업, 새 세션 교체, 이벤트 구독, 종료 시 재시도 취소 | 선택적 ReconnectingKableClient | 재시도 횟수·간격·장비 상태 확인 |
| 앱 종료 | 새 작업 차단, 폴링 취소·await, 대기 요청 종료, 연결 해제 | DisposeAsync와 세션 정리 | 앱 소유 작업·이벤트 구독 정리 |
| 통신 진단과 화면 연결 | 송수신 추적, 진단 전달 경로, UI와 통신 처리 분리 | ICommObserver, 선택적 WPF·OpenTelemetry 패키지 | 보관 정책·화면 갱신·UI dispatch |

이 표의 API를 모두 설정해야 하는 것은 아니다. 문자열 질의는 Simple, 폴링·캐시는 Profile,
사용자 정의 바이너리 프레임은 Builder와 코덱처럼 목적에 맞는 진입점을 선택한다.

## 3. 복잡해지는 이유: 기능들이 한 연결에서 만난다

상태 폴링 중 사용자가 수동 조회를 누르고, 같은 순간 장비가 알람을 보낸다고 가정한다.
그때 선행 요청이 취소되면 다음을 함께 처리해야 한다.

1. 폴링 요청과 수동 요청을 FIFO로 순서대로 처리하거나 CID로 구분한다.
2. 알람을 정상 요청의 응답으로 소비하지 않고 이벤트 경로로 전달한다.
3. 취소된 요청의 대기 작업을 완료하고 늦은 응답이 다른 요청에 연결되지 않게 한다.
4. 연결이 종료되면 나머지 대기자·폴링·재연결 작업의 수명을 정리한다.
5. 복구 후에는 새 세션으로 통신하며 이전 명령을 장비 상태 확인 없이 반복하지 않는다.

각 기능은 개별적으로 작성할 수 있지만, 추가한 타이머·수신 루프·락·응답 큐·재연결 작업이
같은 수명과 오류 정책을 공유하도록 설계하고 검증하는 부분이 커진다.
Kable은 이 공통 동작을 라이브러리에 모아 앱에서 장비 설정과 업무 판단을 작성하도록 돕는다.

### 기본 API 기반으로 구성할 때의 모듈 예시

다음은 구현 책임을 나누는 설계 예시다. 새 기능 구현을 요구하거나 이미 구현된 샘플을 뜻하지 않는다.

```text
DeviceCommunication/
  Transports/
    ConnectionFactory.cs
    ConnectionLifetime.cs
  Codecs/
    FrameDecoder.cs
    FrameEncoder.cs
  Engine/
    RequestScheduler.cs
    ResponseRouter.cs
    RequestLifetime.cs
    EventDispatcher.cs
    PollingAndCache.cs
    RecoveryCoordinator.cs
    ShutdownAndDiagnostics.cs
```

예를 들어 ResponseRouter는 응답 ID 매칭만 구현해서 끝나지 않는다.
타임아웃 제거와 응답 도착이 동시에 일어나도 한 대기 작업만 완료해야 하고,
CID 재사용 시 과거 응답을 구분할 프로토콜 규칙도 따라야 한다.
앱 종료와 재연결이 겹쳐 새 연결이 종료 이후 다시 살아나는 상황도 검증 대상이다.

## 4. Kable로 주기 조회·명령·최신값을 함께 구성하기

```csharp
using Kable.Engine.Profiles;

var config = new KableProfileConfig()
    .UseTcp("192.168.0.100", 9000)
    .AddPeriodic("STATUS?", TimeSpan.FromSeconds(1))
    .AddCommand("ReadModel", "MODEL?");
await using var device = await KableProfileManager.ConnectAsync(config);
Action<string, string> onData = (command, value) =>
    Console.WriteLine($"{command}: {value}");
device.PeriodicDataReceived += onData;
try
{
    Console.WriteLine(await device.ExecuteAsync("ReadModel"));
    await Task.Delay(TimeSpan.FromSeconds(10));
    if (device.TryGetFresh("STATUS?", TimeSpan.FromSeconds(3), out var status))
        Console.WriteLine(status);
}
finally
{
    device.PeriodicDataReceived -= onData;
}
```

이 예제는 Profile에 명령과 간격을 등록하고 이벤트와 유효한 캐시 값을 사용하는 10초 데모다.
첫 폴링 응답이 아직 없으면 TryGetFresh는 false를 반환할 수 있다.
기본 API로 같은 동작을 구성하면 폴링 작업·명령 직렬화·응답 분배·캐시·종료 처리를
함께 구현해야 한다. [시작 가이드](GETTING_STARTED.md)에서 각 API의 사용 범위를 확인한다.

## 5. 같은 기능을 직접 구현했다면 필요한 검증

| 추가한 기능 | 함께 확인할 상황 |
| --- | --- |
| 프레임 파싱 | 한 프레임의 분할 수신, 여러 프레임의 동시 수신, 잘못된 길이와 최대 크기 |
| 요청 라우팅 | 동시 호출, 역순 CID 응답, 응답과 알람의 혼합 |
| 취소·제한시간 | 송신 전 취소, 게이트 대기 중 취소, 송신 후 취소, 늦은 응답 |
| 연결 수명 | 원격 종료 시 전체 대기 완료, Dispose와 수신/송신의 경합 |
| 폴링·캐시 | 첫 값 미수신, 오래된 값, 소비 지연, 명령과 폴링의 동시 실행 |
| 재연결 | 새 세션 교체, 재시도 소진, 종료 중 복구, 이전 명령 중복 전송 방지 |

이 표는 동일 기능을 구축할 때의 검증 계획이다. 이번 비교 실행으로 모든 항목을 검증했다는 뜻은 아니다.
[오류 처리 비교](benchmarks/FaultComparison.md)는 늦은 응답·취소·명시적 원격 종료 등
실제 실행한 범위와 개별 관측을 기록한다. 동일 보호 정책을 추가한 기본 API 예제도 공통 기준을 만족했다.
Kable의 가치는 이런 정책을 앱마다 다시 작성하는 작업을 줄여주는 데 있다.

관련 문서: [실패 처리와 복구](ERROR_HANDLING.md), [세션 수명 계약](CONNECTION_LIFECYCLE.md),
[성능 측정과 검증 범위](PERFORMANCE_BASELINE.md).
