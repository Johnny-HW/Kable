<!-- Kable Architecture Overview Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0284c7 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
      <span>🏛️ 3-TIER LAYERED TOPOLOGY</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      01. Architecture Overview
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Core design philosophy, hybrid transaction routing, and formal class hierarchy combining <strong>Microsoft Bedrock's transport abstraction</strong> with <strong>RSocket reactive interaction patterns</strong>.
    </p>
    <div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
      <span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Bedrock Transport</span>
      <span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">RSocket Interaction</span>
      <span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Hybrid Transaction Router</span>
      <span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast Watchdog</span>
    </div>
  </div>

  <!-- 3-Tier Layer Summary Cards -->
  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #2563eb;">
      <div style="font-weight: 700; color: #1d4ed8; font-size: 14px; margin-bottom: 6px;">1. Upper: RSocket Interaction</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        Provides <code>RequestAsync</code>, <code>SendAsync</code>, <code>Stream</code>, and <code>SendUrgentAsync</code> (E-STOP) contracts on <code>IDeviceSession&lt;T&gt;</code>.
      </div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
      <div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">2. Middle: Protocol Codec</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        Bidirectional zero-allocation framing and slicing (<code>ReadOnlySequence&lt;byte&gt;</code>) via <code>IProtocolCodec&lt;T&gt;</code>.
      </div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #7c3aed;">
      <div style="font-weight: 700; color: #6d28d9; font-size: 14px; margin-bottom: 6px;">3. Lower: Bedrock Transport</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">
        Unifies TCP, RS-232/485 serial, and NamedPipe IPC under a <code>PipeReader Input</code> and <code>PipeWriter Output</code> context.
      </div>
    </div>
  </div>

  <!-- Section 1: Core Architectural Philosophy -->
  <div style="margin-bottom: 30px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. Core Architectural Philosophy
    </h2>

    <p style="font-size: 13.5px; color: #334155; line-height: 1.6;">
      <code>Kable</code> solves the four classic failure modes of industrial hardware software: <strong>thread starvation, heap fragmentation, interleaved response pollution, and UI stuttering</strong>.
    </p>

    <!-- Key Innovation Callout -->
    <div style="background: #f0fdf4; border-left: 4px solid #10b981; border-radius: 0 8px 8px 0; padding: 14px 18px; margin-bottom: 20px;">
      <div style="font-weight: 700; color: #047857; font-size: 13.5px; margin-bottom: 4px;">💡 Hybrid Transaction Router & Fail-Fast Safety</div>
      <ul style="margin: 0; padding-left: 18px; font-size: 12.5px; color: #334155; line-height: 1.6;">
        <li><strong>Preemptive FIFO Lock (SemaphoreSlim)</strong>: For legacy devices without correlation IDs, concurrent requests are serialized safely without frame collisions.</li>
        <li><strong>Lock-Free Interleaving</strong>: For modern protocols with correlation tokens, requests are multiplexed concurrently at microsecond latencies.</li>
        <li><strong>Fail-Fast Disconnection</strong>: Physical cable disconnect immediately fires <code>DeviceDisconnectedException</code>, driving equipment to safe-state without blind retries.</li>
      </ul>
    </div>
  </div>

  <!-- Section 2: Architecture Class Diagram -->
  <div style="margin-bottom: 30px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      2. Integrated Architecture Class Diagram
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
