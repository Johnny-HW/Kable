# Kable 세션 수명 주기 및 장애 복구 가이드 (Connection Lifecycle)

작성일: 2026-10-01  
대상: Kable 세션(`KableSession`, `KableSimple`)을 사용하는 하드웨어 제어 소프트웨어 엔지니어

---

## 1. 개요 및 핵심 설계 원칙

Kable은 반도체 및 FA 장비 제어의 **엄격한 명령-응답 정합성(Safety & Predictability)**을 최우선으로 설계되었습니다.  
특히 명령과 응답을 순차적으로 1:1 교환하는 **FIFO 모드**에서는, 네트워크 지연이나 장비 이상으로 인해 지연된 응답(Late Response)이 다음 명령의 응답으로 잘못 매칭되는 치명적인 제어 사고를 방지하기 위해 **Fail-Fast & Safe-Close** 계약을 따릅니다.

---

## 2. 장애 유형별 예외 및 세션 상태 매트릭스

| 장애 시점 / 유형 | 발생 예외 | 세션 상태 | 세션 재사용 가능 여부 | 권장 복구 방법 |
| :--- | :--- | :--- | :--- | :--- |
| **요청 전 취소** (호출 시점 CancellationToken 이미 취소) | `OperationCanceledException` | 유지 (`Running`) | **가능** (동일 객체 계속 사용) | 세션 유지, 다음 작업 진행 |
| **FIFO 락 획득 대기 중 취소** (선행 명령 처리 대기 중 타임아웃/취소) | `DeviceTimeoutException` 또는 `OperationCanceledException` | 유지 (`Running`) | **가능** (동일 객체 계속 사용) | 세션 유지, 큐 백로그 확인 후 재시도 |
| **명령 송신 후 응답 대기 중 타임아웃** (장비 응답 없음) | `DeviceTimeoutException` | **종료 (`Stopping` → `Stopped`)** | **불가** (세션 영구 닫힘) | 기존 클라이언트 폐기(`DisposeAsync`) 후 새 인스턴스 연결 및 이벤트 재구독 |
| **명령 송신 후 응답 대기 중 취소** (CancellationToken 트리거) | `OperationCanceledException` | **종료 (`Stopping` → `Stopped`)** | **불가** (세션 영구 닫힘) | 기존 클라이언트 폐기(`DisposeAsync`) 후 새 인스턴스 연결 및 이벤트 재구독 |
| **물리적 연결 단절** (케이블 탈락, 소켓 리셋) | `DeviceDisconnectedException` | **종료 (`Stopping` → `Stopped`)** | **불가** (세션 영구 닫힘) | 물리 연결 확인 후 새 인스턴스 연결 |
| **상관 ID (CID) 모드 타임아웃** (다중화 프로토콜) | `DeviceTimeoutException` | 유지 (`Running`) | **가능** (해당 CID만 실패 처리) | 실패한 명령만 개별 재시도 |

> [!IMPORTANT]
> **왜 FIFO 모드에서 응답 대기 중 타임아웃이 발생하면 세션이 종료되는가?**  
> 하드웨어에 이미 커맨드가 송신된 상태에서 타임아웃으로 상위 호출을 반환해 버리면, 몇 초 뒤 장비가 뒤늦게 보낸 응답이 다음에 실행될 다른 커맨드의 응답으로 소비(Misattribution)될 위험이 있습니다.  
> Kable은 이를 원천 차단하기 위해 **단일 수명 계약(Single-Life-Cycle Contract)**을 적용하여 세션을 닫고 파이프라인 버퍼를 완전히 초기화하도록 강제합니다.

---

## 3. 타임아웃 및 단절 시 올바른 복구 패턴 (C# Recipe)

### 선택적인 자동 재연결

```csharp
using Kable.Simple;
using Kable.Simple.Reconnecting;

await using var client = await ReconnectingKableClient.OpenAsync(
    ct => KableSimple.OpenTcpAsync("192.168.0.100", 9000, ct: ct),
    new ReconnectOptions
    {
        MaxAttempts = 3,
        RetryDelay = TimeSpan.FromSeconds(1),
        ConnectTimeout = TimeSpan.FromSeconds(5)
    });
client.LineReceived += line => Console.WriteLine(line);
client.ErrorOccurred += error => Console.WriteLine(error.Message);
client.Reconnected += () => Console.WriteLine("새 연결로 복구 완료");
string status = await client.QueryAsync("STATUS");
```

- 최초 연결 실패는 호출자에게 반환한다. 이후 단절은 새 클라이언트로 복구한다.
- 단절 감지는 RetryDelay마다 수행한다. 이후 시도 사이에도 동일 간격을 적용한다.
- 단절당 MaxAttempts만큼 시도하고, 성공하면 다음 단절의 시도 횟수를 초기화한다.
- 연결 팩토리는 취소 토큰을 준수하고 매번 새 연결된 클라이언트를 반환해야 한다. 연결 제한시간은 협력적 취소에 의존한다.
- 이벤트 구독은 래퍼에 유지되고, 이전 클라이언트는 해제한다.
- 복구 중 들어온 명령은 DeviceDisconnectedException으로 실패한다. 명령을 큐에 보관하거나 재전송하지 않는다.
- 재시도가 소진되면 RecoveryTask가 완료되며 LastError로 실패 원인을 확인한다. 다시 시도하려면 래퍼를 폐기하고 새로 연다.
- OpenAsync의 ct는 최초 연결에 적용된다. 반환된 래퍼의 수명 종료는 DisposeAsync로 수행한다.
- await using으로 종료하면 진행 중 연결 시도의 취소와 워커 완료를 기다린다. 이벤트 핸들러에서 종료를 동기 대기하지 않는다.
- 쿼리 예외는 호출자가 직접 처리한다. 재연결이 장비 동작 성공 여부를 판단해 주지는 않는다.

컴파일되는 사용 예제는 samples/Kable.QuickStart/AdvancedUsage.cs에 있다.

---
## 4. UI 스레드 연동 및 이벤트 수신 주의사항

1. **`LineReceived` 호출 컨텍스트**
   - `LineReceived` 이벤트는 I/O 백그라운드 파이프라인 스레드 풀에서 디스패치됩니다.
   - WPF 등 GUI 프레임워크에서 컨트롤을 직접 갱신하면 크로스 스레드 예외가 발생하므로 `Dispatcher.InvokeAsync` 등을 거쳐야 합니다.
2. **`QueryAsync` 응답과 `LineReceived`의 분리**
   - `QueryAsync`로 요청-응답 매칭되어 소비된 라인은 `LineReceived`로 전달되지 않습니다.
   - 송수신되는 모든 로우(Raw) 패킷 흐름을 감사하거나 UI 터미널로 보려면 `ICommObserver`를 빌더에 등록하여 관찰해야 합니다.
