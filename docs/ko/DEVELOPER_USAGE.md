# 개발자 API 사용 가이드

## 기본 제한시간과 타입 안전한 요청

```csharp
using Kable.Codecs;
using Kable.Configuration;
using Kable.Extensions;

var device = new KableDeviceOptions
{
    Host = "192.168.0.100", Port = 9000,
    ConnectTimeoutMs = 5000, DefaultRequestTimeoutMs = 1500
};
await using var session = new KableClientBuilder<string>()
    .UseOptions(device).UseCodec(new AsciiLineCodec()).Build();
await session.StartAsync();
string status = await session.RequestAsync("STATUS"); // 1500ms 기본값
string version = await session.RequestAsync("VERSION", TimeSpan.FromSeconds(2));
```

`RequestAsync` 확장 메서드는 세션과 동일한 메시지 타입으로 응답을 반환한다. 기존 `RequestAsync<TResponse>(request, timeout, ct)`는 그대로 유지되며 전달한 제한시간만 사용한다. 확장 메서드는 `IRequestTimeoutProvider`를 구현하지 않은 외부 세션에 3초 기본값을 사용한다.

`KableSimple.OpenAsync(device)`와 `KableSimple.FromSessionAsync(session)`도 기본 요청 제한시간을 사용한다. `OpenAsync(device, simpleOptions)`에서는 `simpleOptions.DefaultTimeout`이 우선한다. 연결 설정은 `device`에서 가져온다. `OpenTcpAsync(host, port, simpleOptions)`는 `simpleOptions.ConnectTimeoutMs`를 사용한다.

| 설정 | 적용 범위 | 우선순위/기본값 |
| --- | --- | --- |
| `TimeoutMs` | 이전 설정과 호환되는 연결 제한시간 | `ConnectTimeoutMs`를 생략했을 때 사용, 기본 3000ms |
| `ConnectTimeoutMs` | DeviceOptions의 TCP·Named Pipe 연결 | 명시한 값이 `TimeoutMs`보다 우선, 0 이하 거부 |
| `DefaultRequestTimeoutMs` | 제한시간 없는 요청 확장 메서드·Simple 래퍼 | 기본 3000ms, 0 이하 거부 |
| Simple의 `DefaultTimeout` | `QueryAsync` | 기본 3초, 명시적 질의 제한시간이 우선 |
| Simple의 `ConnectTimeoutMs` | `OpenTcpAsync` 연결 | 기본 0은 기존 전송 기본값, 음수 거부 |

시리얼 연결에는 DeviceOptions의 연결 제한시간이 적용되지 않는다. 기존 `OpenNamedPipeAsync`는 메서드의 `timeoutMs` 인자를 사용한다.

## 빌더의 고급 설정

`UseSessionOptions(new KableSessionOptions<string> { ... })`로 수신 큐와 알람 정책을 설정하고, `UseHeartbeat(new HeartbeatOptions<string>(...))`로 하트비트를 설정한다. 모두 `Build()` 전에 지정한다. 옵션 객체는 세션에 전달되므로 빌드 후 임의로 변경하지 않는다.

하트비트는 장비 프로토콜에 맞는 ping과 pong 판별을 지정해야 한다. 상관 ID가 없는 장비에서 일반 명령 응답과 pong을 구분하지 않으면 오매칭 가능성이 있다. 지원하지 않는 장비에는 활성화하지 않는다.

컴파일되는 예제는 `samples/Kable.QuickStart/AdvancedUsage.cs`에서 관리한다.

## 응답 분류와 이벤트 계약

`KableSimpleOptions.IsAutonomousMessage`로 장비의 자발 이벤트 규칙을 지정한다. null이면 기존 `$`, `#`, `!`, `*` 접두사 규칙을 사용한다. 정상 응답이 `!OK`인 장비에서는 예를 들어 `message => message.StartsWith("$")`를 지정한다. 세션의 별도 알람 판별은 그대로 적용된다.

`LineReceived`는 질의 응답으로 소비되지 않은 라인을 백그라운드에서 전달한다. 질의 응답은 `QueryAsync` 반환값에서 처리한다. 핸들러는 짧게 유지하고, 전체 통신 추적은 옵저버를 사용한다.

WPF 화면을 갱신할 때는 UI 스레드에 전달한다. 아래 예제의 `window`와 `viewModel`은 상위 앱에서 소유한다.

```csharp
Action<string> onLine = line =>
{
    _ = window.Dispatcher.InvokeAsync(() => viewModel.LastLine = line);
};
client.LineReceived += onLine;
// 화면 종료 시:
client.LineReceived -= onLine;
```

비동기 작업 실패는 해당 작업을 await해서 처리한다. `ErrorOccurred`는 모든 `QueryAsync` 실패를 대신 받는 이벤트가 아니다. 이벤트에서 `.Wait()`나 동기 종료 대기를 사용하지 않는다.

## 자동 재연결

[연결 수명과 복구](CONNECTION_LIFECYCLE.md)를 참고한다. 자동 재연결은 명시적으로 `ReconnectingKableClient`를 사용하는 경우에만 동작한다. 기존 Simple 클라이언트의 수명 계약은 유지된다.

진행 중 이동·쓰기 명령은 실패로 호출자에게 반환된다. 재연결이 완료돼도 같은 명령을 자동 실행하지 않는다. 실제 장비 상태를 조회한 뒤 상위 시퀀스가 후속 동작을 결정해야 한다.
