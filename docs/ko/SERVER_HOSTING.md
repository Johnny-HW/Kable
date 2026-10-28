# 다중 클라이언트 서버 호스팅

Kable 1.10.0은 기존 Kable/Core 패키지에 `IKableServer<TMessage>`,
`IServerSession<TMessage>`, `KableServerBuilder<TMessage>`를 추가한다.
전송 → 코덱 → 엔진의 계층은 유지한다.

## TCP 서버 시작

```csharp
using System.Net;
using Kable.Builder;
using Kable.Codecs;

await using var server = new KableServerBuilder<string>()
    .ListenTcp(IPAddress.Loopback, 9000)
    .UseCodec(() => new AsciiLineCodec())
    .ConfigureOptions(options =>
    {
        options.MaxConnections = 100;
        options.SessionQueueCapacity = 256;
        options.DefaultBroadcastTimeout = TimeSpan.FromSeconds(1);
        options.DefaultShutdownTimeout = TimeSpan.FromSeconds(3);
    })
    .Build();

server.ClientConnected += (_, e) => Console.WriteLine($"연결: {e.Session.ConnectionId}");
server.MessageReceived += (_, e) => Console.WriteLine($"{e.Session.ConnectionId}: {e.Message}");
server.ClientDisconnected += (_, e) => Console.WriteLine($"종료: {e.Reason}");
await server.StartAsync();
Console.ReadLine();
await server.StopAsync();
```

Build 시 TCP 포트를 바인딩하고 StartAsync에서 접속 수락을 시작한다. 빌더는 한 번만 사용한다.
`WithListener`로 IPC 등 기존 `IConnectionListener`를 전달할 수 있다.
빌드된 서버가 리스너와 수락한 연결의 해제를 소유한다.
코덱 팩토리는 연결마다 새 인스턴스를 반환해야 하며, Encode와 TryDecode는 동시에 실행될 수 있다.
임시 포트는 `TcpConnectionListener(IPAddress.Loopback, 0)`의 LocalEndPoint를 읽고
해당 리스너를 WithListener에 전달한다.

## 개별 전송과 브로드캐스트

```csharp
if (server.TryGetSession(connectionId, out var session) && session != null)
    await session.SendAsync("STATUS", ct);
await server.SendAsync(connectionId, "STATUS", ct);
var result = await server.BroadcastAsync("NOTICE", ct);
Console.WriteLine($"큐 등록: {result.EnqueuedCount}/{result.TotalSessions}");
foreach (var failedId in result.FailedConnectionIds)
    Console.WriteLine($"등록 실패: {failedId}");
```

SendAsync의 성공은 세션 송신 큐 등록을 뜻한다. 실제 네트워크 전달이나 장비 실행 완료를 보증하지 않는다.
서버의 SendAsync에서 연결 ID가 없으면 KeyNotFoundException이 발생한다.
BroadcastResult는 세션 스냅샷에 대한 큐 등록 결과다. 실행 중 접속 목록이 달라질 수 있다.
브로드캐스트 제한시간이 지나면 실패 ID를 반환하고, 호출자 취소는 OperationCanceledException을 발생시킨다.
이미 등록한 메시지는 이후 취소해도 회수하지 않는다.
종료된 서버는 SendAsync/BroadcastAsync를 거부하며, 종료된 세션도 새 전송을 거부한다.
ActiveSessions는 스냅샷이다. TryGetSession 성공이 이후 SendAsync 성공을 보장하지 않는다.

## 용량과 종료 계약

| 옵션 | 기본값 | 의미 |
| --- | --- | --- |
| MaxConnections | 10,000 | 초과 접속은 코덱 생성 전에 종료 |
| SessionQueueCapacity | 1,000 | 세션별 정확한 대기 메시지 수. 송신 중 메시지 1개가 추가로 존재할 수 있음 |
| DefaultBroadcastTimeout | 1초 | 전체 스냅샷의 큐 등록에 공통 적용되는 제한시간 |
| DefaultShutdownTimeout | 3초 | 송신 큐 드레인 후 남은 연결을 강제 종료하기까지의 시간 |

옵션은 생성 시 검증·복사한다. 용량은 양수, 시간은 양수이면서 Int32.MaxValue 밀리초 이하여야 한다.
큐는 바이트가 아닌 메시지 개수를 제한한다. 문자열 변환, 이벤트 인자, 세션 스냅샷과
브로드캐스트는 할당을 수행하므로 서버 전체 경로가 0-GC인 것은 아니다.

StopAsync는 접속 수락과 새 전송을 막고 기존 큐를 비운 뒤 연결을 종료한다.
막힌 Flush는 드레인 제한시간 이후 Abort한다. 그 뒤 자원 정리가 이어지므로 전체 종료 시간이
드레인 제한시간보다 길 수 있다. 커스텀 리스너는 Stop 시 AcceptAsync를 깨워야 하며,
연결 컨텍스트는 Abort 시 I/O를 깨우고 DisposeAsync를 완료해야 한다.
StopAsync 호출자의 취소는 해당 대기만 취소하며 공유 정리는 계속 진행한다.
동시 Stop/Dispose는 정리를 공유하며 DisposeAsync는 정리와 리스너 해제를 기다린다.
시작 전 Stop도 서버 수명을 끝낸다. 종료 후에는 새 서버를 만든다.
StartAsync의 토큰은 진입 시 취소를 검사하며 실행 중 서버 수명을 소유하지 않는다.

이벤트는 백그라운드 경로에서 호출한다. 짧게 처리하고 UI 갱신은 Dispatcher 등으로 전환한다.
이벤트 내부에서 StopAsync/DisposeAsync를 동기 대기하지 않는다.
구독자 하나가 예외를 던져도 Trace에 기록하고 나머지 구독자를 호출한다.
async void 핸들러는 기다리거나 예외를 보호하지 않는다. 비동기 처리는 앱이 소유한 유한 워커로 넘긴다.
코덱 장애는 해당 세션만 종료하고 ClientDisconnected의 Reason으로 전달한다.
접속 수락 실패는 Trace에 기록하고 서버를 종료한다. IsRunning과 수명 이벤트를 관찰한다.
이 빌더는 서버 요청/응답 상관 처리, 인증, TLS 협상을 제공하지 않는다.
해당 기능이 필요하면 적합한 커스텀 전송·프로토콜을 사용한다.

PacketTraceRecord에 명시적 연결 ID 생성자와 nullable ConnectionId 속성이 추가된다.
기존 생성자는 유지하며 ConnectionId는 null이다. 서버가 자동으로 관찰자 기록을 발행하지는 않는다.

실제 패키지 기반 TCP 수신·브로드캐스트·종료 예제는
`tools/release/Consumers/Console/ServerProbe.cs`이며 .NET 8/10에서 실행 검증한다.
