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
| **명령 송신 후 응답 대기 중 타임아웃** (장비 응답 없음) | `DeviceTimeoutException` | **종료 (`Closed`/`Disposed`)** | **불가** (세션 영구 닫힘) | 기존 클라이언트 폐기(`DisposeAsync`) 후 새 인스턴스 연결 및 이벤트 재구독 |
| **명령 송신 후 응답 대기 중 취소** (CancellationToken 트리거) | `OperationCanceledException` | **종료 (`Closed`/`Disposed`)** | **불가** (세션 영구 닫힘) | 기존 클라이언트 폐기(`DisposeAsync`) 후 새 인스턴스 연결 및 이벤트 재구독 |
| **물리적 연결 단절** (케이블 탈락, 소켓 리셋) | `DeviceDisconnectedException` | **종료 (`Faulted`/`Closed`)** | **불가** (세션 영구 닫힘) | 물리 연결 확인 후 새 인스턴스 연결 |
| **상관 ID (CID) 모드 타임아웃** (다중화 프로토콜) | `DeviceTimeoutException` | 유지 (`Running`) | **가능** (해당 CID만 실패 처리) | 실패한 명령만 개별 재시도 |

> [!IMPORTANT]
> **왜 FIFO 모드에서 응답 대기 중 타임아웃이 발생하면 세션이 종료되는가?**  
> 하드웨어에 이미 커맨드가 송신된 상태에서 타임아웃으로 상위 호출을 반환해 버리면, 몇 초 뒤 장비가 뒤늦게 보낸 응답이 다음에 실행될 다른 커맨드의 응답으로 소비(Misattribution)될 위험이 있습니다.  
> Kable은 이를 원천 차단하기 위해 **단일 수명 계약(Single-Life-Cycle Contract)**을 적용하여 세션을 닫고 파이프라인 버퍼를 완전히 초기화하도록 강제합니다.

---

## 3. 타임아웃 및 단절 시 올바른 복구 패턴 (C# Recipe)

### KableSimple 복구 패턴

```csharp
using Kable.Exceptions;
using Kable.Simple;

public class RobotDeviceManager : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private IKableSimpleClient? _client;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

    public RobotDeviceManager(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        await _reconnectLock.WaitAsync(ct);
        try
        {
            if (_client != null && _client.IsConnected) return;

            // 1. 기존 리소스가 있다면 안전하게 해제
            if (_client != null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            // 2. 새 클라이언트 인스턴스 생성 및 연결
            var client = await KableSimple.OpenTcpAsync(_host, _port, ct: ct);

            // 3. 자발 이벤트 및 단절 이벤트 재구독
            client.LineReceived += OnLineReceived;
            client.Disconnected += OnDisconnected;

            _client = client;
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    public async Task<string> QueryAsync(string command, TimeSpan timeout, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);

        try
        {
            return await _client!.QueryAsync(command, timeout, ct);
        }
        catch (DeviceTimeoutException ex)
        {
            // 타임아웃 발생 시 세션이 종료되므로 _client 인스턴스를 무효화하고 상위로 전달
            _client = null;
            throw new InvalidOperationException($"장비 응답 타임아웃: {ex.Message}. 세션이 안전하게 종료되었습니다.", ex);
        }
        catch (DeviceDisconnectedException ex)
        {
            _client = null;
            throw new InvalidOperationException($"장비 연결 단절: {ex.Message}", ex);
        }
    }

    private void OnLineReceived(string line)
    {
        // 백그라운드 스레드에서 수신되므로 UI 갱신 시 Dispatcher 사용 필요
    }

    private void OnDisconnected(Exception? reason)
    {
        _client = null; // 재연결 대상 표시
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _reconnectLock.Dispose();
    }
}
```

---

## 4. UI 스레드 연동 및 이벤트 수신 주의사항

1. **`LineReceived` 호출 컨텍스트**
   - `LineReceived` 이벤트는 I/O 백그라운드 파이프라인 스레드 풀에서 디스패치됩니다.
   - WPF 등 GUI 프레임워크에서 컨트롤을 직접 갱신하면 크로스 스레드 예외가 발생하므로 `Dispatcher.InvokeAsync` 등을 거쳐야 합니다.
2. **`QueryAsync` 응답과 `LineReceived`의 분리**
   - `QueryAsync`로 요청-응답 매칭되어 소비된 라인은 `LineReceived`로 전달되지 않습니다.
   - 송수신되는 모든 로우(Raw) 패킷 흐름을 감사하거나 UI 터미널로 보려면 `ICommObserver`를 빌더에 등록하여 관찰해야 합니다.
