<!-- Kable Architecture Overview Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0284c7 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🏛️ 3계층 계층화 토폴로지 (3-TIER LAYERED TOPOLOGY)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      01. 아키텍처 개요 (Architecture Overview)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      <strong>마이크로소프트 Bedrock 전송 추상화</strong>와 <strong>RSocket 반응형 인터랙션 패턴</strong>을 융합한 핵심 설계 철학, 하이브리드 트랜잭션 라우팅 및 정형 클래스 계층 구조입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Bedrock Transport</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">RSocket Interaction</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">하이브리드 트랜잭션 라우터</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast 워치독</span>
</div>
</div>

<!-- 3-Tier Layer Summary Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 14px; margin-bottom: 6px;">1. 상위 계층: RSocket 반응형 인터랙션</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        <code>IDeviceSession&lt;T&gt;</code>를 통해 <code>RequestAsync</code>, <code>SendAsync</code>, <code>Stream</code>, 그리고 긴급 정지용 <code>SendUrgentAsync</code>(E-STOP) 규약을 제공합니다.
</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">2. 중간 계층: 프로토콜 코덱 (Protocol Codec)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        <code>IProtocolCodec&lt;T&gt;</code>를 통해 양방향 Zero-Allocation 프레이밍 및 메모리 복사 없는 슬라이싱(<code>ReadOnlySequence&lt;byte&gt;</code>)을 수행합니다.
</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 14px; margin-bottom: 6px;">3. 하위 계층: Bedrock 전송 추상화 (Transport)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        TCP, RS-232/485 시리얼, NamedPipe IPC를 단일 <code>PipeReader Input</code> 및 <code>PipeWriter Output</code> 컨텍스트로 일원화합니다.
</div>
</div>
</div>

<!-- Section 1: Core Architectural Philosophy -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. 핵심 아키텍처 설계 철학
</h2>

<p style="font-size: 13.5px; color: #334155; line-height: 1.6;">
<code>Kable</code>은 산업용 하드웨어 소프트웨어에서 발생하는 4대 고질적 결함인 <strong>스레드 고갈(Thread Starvation), 힙 메모리 파편화(Heap Fragmentation), 응답 데이터 혼선 및 교차 오염(Interleaved Response Pollution), UI 멈춤/스터터링(UI Stuttering)</strong>을 원천 해결합니다.
</p>

<!-- Key Innovation Callout -->
<div style="background: #f0fdf4; border-left: 4px solid #10b981; border-radius: 0 8px 8px 0; padding: 14px 18px; margin-bottom: 20px;">
<div style="font-weight: 700; color: #047857; font-size: 13.5px; margin-bottom: 4px;">💡 하이브리드 트랜잭션 라우터 & Fail-Fast 안전성</div>
<ul style="margin: 0; padding-left: 18px; font-size: 12.5px; color: #334155; line-height: 1.6;">
<li><strong>선점형 FIFO 락 (SemaphoreSlim)</strong>: Correlation ID(연관 식별자)를 지원하지 않는 레거시 장비 통신 시, 동시 호출 요청을 안전하게 직렬화하여 프레임 충돌을 원천 차단합니다.</li>
<li><strong>Lock-Free 인터리빙 멀티플렉싱</strong>: Correlation 토큰이 포함된 최신 프로토콜 통신 시, 마이크로초(µs) 단위 지연 시간으로 여러 요청을 동시 다중화 처리합니다.</li>
<li><strong>Fail-Fast 연결 단절 전파</strong>: 물리 케이블 단절 즉시 <code>DeviceDisconnectedException</code>을 모든 대기자에게 예외 전파하여, 맹목적인 재시도 없이 설비를 즉각 안전 상태(Safe-State)로 전이합니다.</li>
</ul>
</div>
</div>

<!-- Section 2: Architecture Class Diagram -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      2. 통합 아키텍처 클래스 다이어그램
</h2>

```mermaid
classDiagram
    %% Transport Layer
    class IConnectionContext {
        <<interface>>
        +string ConnectionId
        +string EndpointDescription
        +PipeReader Input
        +PipeWriter Output
        +CancellationToken ConnectionClosed
        +Abort(string reason)
    }
    class TcpConnectionContext {
        -Socket _socket
        -NetworkStream _stream
    }
    class SerialPortConnectionContext {
        -SerialPort _serialPort
    }
    class NamedPipeConnectionContext {
        -NamedPipeClientStream _pipe
    }
    IConnectionContext <|.. TcpConnectionContext
    IConnectionContext <|.. SerialPortConnectionContext
    IConnectionContext <|.. NamedPipeConnectionContext

    %% Codec Layer
    class IProtocolCodec~T~ {
        <<interface>>
        +bool SupportsCorrelationId
        +bool TryDecode(ref ReadOnlySequence buffer, out T message)
        +void Encode(T message, IBufferWriter output)
        +string ExtractCorrelationId(T message)
        +bool IsAutonomousMessage(T message)
    }
    class AsciiLineCodec {
        -byte _delimiter
        -int _maxFrameSize
    }
    IProtocolCodec <|.. AsciiLineCodec

    %% Session Layer
    class IDeviceSession~T~ {
        <<interface>>
        +bool IsConnected
        +IAsyncEnumerable~T~ Stream
        +ValueTask SendAsync(T message)
        +ValueTask~TResponse~ RequestAsync(T request, TimeSpan timeout)
        +ValueTask SendUrgentAsync(T urgentMessage)
    }
    class KableSession~T~ {
        -IConnectionContext _context
        -IProtocolCodec~T~ _codec
        -SemaphoreSlim _fifoLock
        -ConcurrentDictionary _pendingRequests
    }
    IDeviceSession <|.. KableSession
    KableSession o-- IConnectionContext
    KableSession o-- IProtocolCodec
```

</div>

</div>
