# 실패했을 때와 종료할 때

문자열 질의의 정상 흐름은 [처음 연결하는 순서](ko/GETTING_STARTED.md)를 참고한다.
아래 예제의 주소와 명령은 실제 장비에 맞게 바꾼다.

## 실패별 다음 행동

| 상황 | 호출자에게 보이는 결과 | 다음 행동 |
| --- | --- | --- |
| 최초 연결 실패 | 연결 메서드의 예외 | 주소·포트·장비 상태와 전송별 설정 확인 |
| FIFO 송신 후 응답 timeout | `DeviceTimeoutException`, 연결 종료 | 기존 클라이언트 폐기 후 새 연결; 장비가 명령을 실행했는지 확인 |
| FIFO 송신 후 사용자 취소 | `OperationCanceledException`, 연결 종료 | 취소된 앱 작업 종료; 재개 시 새 연결 |
| 요청 전 취소/FIFO 락 대기 중 취소 | 취소 예외, 세션 유지 가능 | 취소 원인 처리; 상세 수명 표 확인 |
| 물리 단절 | 대기 중 요청의 `DeviceDisconnectedException` | 연결 복구와 장비 상태 확인 |
| CID 요청 timeout | 해당 요청 실패, 세션 유지 가능 | 프로토콜의 CID 재사용·지연 응답 정책에 따라 처리 |
| Wait 전달 대기열 포화 | `TelemetryBufferOverflowException`, 세션 종료 | 소비 속도·큐 용량 조정 또는 Drop 정책 선택 후 새 세션 생성 |
| 알람 큐/저장 실패 | `AlarmBufferOverflowException`, 세션 종료 | 저장소·소비자와 `SpoolSummary`의 Stored/InDoubt/Unprocessed 확인 |
| 재연결 진행 중 요청 | `DeviceDisconnectedException` | 복구 이벤트/상태 확인 후 다음 작업 결정 |

예외만 보고 장비에서 명령이 실행되지 않았다고 판단하지 않는다.
이동·쓰기·시작 명령은 실제 장비 상태를 확인한 뒤 후속 동작을 결정한다.
수동으로 같은 세션의 `StartAsync`를 다시 호출해 연결을 복구하지 않는다.

## QueryAsync 실패는 await한 곳에서 처리

```csharp
using Kable.Exceptions;
using Kable.Simple;

using var lifetime = new CancellationTokenSource();
try
{
    await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000, ct: lifetime.Token);
    Console.WriteLine(await device.QueryAsync("STATUS?", TimeSpan.FromSeconds(2), lifetime.Token));
}
catch (DeviceTimeoutException ex)
{
    Console.Error.WriteLine($"{ex.Command}: {ex.Timeout} 안에 응답 없음. 새 연결과 장비 상태 확인 필요.");
}
catch (DeviceDisconnectedException ex)
{
    Console.Error.WriteLine($"연결 단절: {ex.Message}");
}
catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
{
    Console.WriteLine("호출자가 작업을 취소했습니다.");
}
```

여기에 잡히지 않는 최초 연결 오류나 다른 예외는 상위 앱에 전달된다.
`ErrorOccurred` 이벤트가 모든 `QueryAsync` 실패를 대신 전달하지 않는다.
이벤트는 백그라운드 수신·이벤트 핸들러 오류 관찰에 사용한다.

## 선택적 자동 재연결

```csharp
using Kable.Simple;
using Kable.Simple.Reconnecting;

using var lifetime = new CancellationTokenSource();
await using var device = await ReconnectingKableClient.OpenAsync(
    ct => KableSimple.OpenTcpAsync("192.168.0.100", 9000, ct: ct),
    new ReconnectOptions
    {
        MaxAttempts = 3,
        RetryDelay = TimeSpan.FromSeconds(1),
        ConnectTimeout = TimeSpan.FromSeconds(5)
    }, lifetime.Token);
Action onReconnected = () => Console.WriteLine("새 연결로 복구되었습니다. 장비 상태를 확인하세요.");
device.Reconnected += onReconnected;
try
{
    Console.WriteLine(await device.QueryAsync("STATUS?", ct: lifetime.Token));
}
finally
{
    device.Reconnected -= onReconnected;
}
```

이 짧은 예제는 복구 옵션과 구독 수명을 보여 준다. 복구를 관찰하려면 앱이 사용하는 동안
래퍼를 유지한다. 최초 연결 실패는 OpenAsync 호출자에게 반환된다.
반환 이후 단절은 새 클라이언트로 복구하며, 실패한 명령을 재전송하거나 큐에 보관하지 않는다.
`Reconnected`는 연결 복구 신호이며 장비가 명령을 성공적으로 수행했다는 신호는 아니다.
재시도 소진은 `RecoveryTask`와 `LastError`로 확인한다.
OpenAsync에 전달한 토큰은 최초 연결에 적용된다. 이후 래퍼 종료는 `DisposeAsync`로 수행한다.

## 종료 순서

1. 앱에서 새 장비 작업을 시작하지 않도록 한다.
2. 앱이 소유한 반복 작업의 토큰을 취소한다.
3. 보관한 반복 작업 Task를 await한다. 예상한 취소만 처리하고 다른 실패는 기록/전파한다.
4. 이벤트 구독을 해제하고 `DisposeAsync` 또는 `await using`으로 클라이언트를 정리한다.

앱의 반복 작업은 [취소 후 await 예제](ko/GETTING_STARTED.md)를 참고한다.
통신 이벤트 안에서 `.Wait()`, `.Result`, 동기 Dispose 대기를 하지 않는다.
이벤트 콜백과 UI 스레드 관계는 [설정과 이벤트 가이드](ko/DEVELOPER_USAGE.md)에 설명한다.

진단에는 오류 종류, 장비 ID/주소, 명령, 연결/요청 제한시간과 `ICommObserver`의 최근
송수신 기록을 함께 남긴다. [관측성 가이드](ko/03_OBSERVABILITY_LOGGING.md)를 참고한다.

## 종료 중 오류

TCP/NamedPipe의 Abort는 즉시 연결을 끊는다. 이후에도 DisposeAsync를 호출해 파이프라인과
취소 자원을 정리한다. 동시 DisposeAsync 호출은 같은 완료 작업과 실패 결과를 공유한다.
정리 단계가 실패해도 나머지 단계는 수행하고 AggregateException으로 실패를 보고한다.

MQTT/OPC UA의 복구 작업이 실패해도 DisposeAsync는 채널·클라이언트·구독·세션과
인증 자원을 정리한 뒤 AggregateException을 반환한다. 이 오류도 종료를 await한 위치에서
기록한다. 구독 채널에는 이미 받은 값이 남을 수 있으므로 정상 완료를 기다리려면 먼저 비운다.
알람 저장 콜백은 전달받은 취소 토큰을 준수해야 기존 저장/종료 제한시간이 작동한다.
