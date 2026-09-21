<!-- Kable Core Interfaces Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>⚙️ API 계약 및 규약 명세 (CONTRACT SPECIFICATION)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      02. 코어 인터페이스 명세서 (Core Interfaces Spec)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      하위 Bedrock 전송 컨텍스트, 중간 Zero-Allocation 프레이밍 코덱, 상위 RSocket 반응형 세션을 위한 정형 API 계약 규약입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IConnectionContext</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IProtocolCodec&lt;T&gt;</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IDeviceSession&lt;T&gt;</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast 예외 체계</span>
</div>
</div>

<!-- Contract 1: IConnectionContext -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 22px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 14px; color: #0369a1;">1. 하위 전송 계층 추상화 (IConnectionContext)</span>
<span style="background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Core</span>
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        물리 통신(TCP, 시리얼) 및 논리 통신(Named Pipe IPC) 채널을 단일 규약으로 대표하는 전이중 Zero-Allocation Pipelines 컨텍스트:
</p>

```csharp
namespace Kable.Core;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

public interface IConnectionContext : IAsyncDisposable
{
    string ConnectionId { get; }
    string EndpointDescription { get; }
    
    // Bedrock 표준 0-GC Pipelines
    PipeReader Input { get; }
    PipeWriter Output { get; }
    
    // 연결 단절 알림 토큰
    CancellationToken ConnectionClosed { get; }
    
    void Abort(string reason);
}
```

</div>
</div>

<!-- Contract 2: IProtocolCodec -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 22px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 14px; color: #059669;">2. Zero-Allocation 프레이밍 코덱 (IProtocolCodec&lt;TMessage&gt;)</span>
<span style="background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Codecs</span>
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        중간 버퍼 복사 없이 유입된 바이트 시퀀스를 도메인 메시지로 변환 및 인코딩:
</p>

```csharp
namespace Kable.Codecs;

using System.Buffers;

public interface IProtocolCodec<TMessage>
{
    bool SupportsCorrelationId { get; }
    bool TryDecode(ref ReadOnlySequence<byte> buffer, out TMessage message);
    void Encode(TMessage message, IBufferWriter<byte> output);
    string? ExtractCorrelationId(TMessage message);
    bool IsAutonomousMessage(TMessage message) => false;
}
```

</div>
</div>

<!-- Contract 3: IDeviceSession -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 22px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 14px; color: #6366f1;">3. 상위 반응형 세션 (IDeviceSession&lt;TMessage&gt;)</span>
<span style="background: #ede9fe; color: #6d28d9; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Engine</span>
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        산업용 장비 제어를 위한 표준 상위 세션 퍼사드(Facade):
</p>

```csharp
namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IDeviceSession<TMessage> : IAsyncDisposable, IDisposable
{
    bool IsConnected { get; }
    
    // 1. 실시간 텔레메트리 스트리밍 채널
    IAsyncEnumerable<TMessage> Stream { get; }
    
    // 2. 단방향 명령 발송 (Fire-and-forget)
    ValueTask SendAsync(TMessage message, CancellationToken ct = default);
    
    // 3. 워치독 격리 기반의 질의-응답 RPC (Request-Response)
    ValueTask<TResponse> RequestAsync<TResponse>(TMessage request, TimeSpan timeout, CancellationToken ct = default);
    
    // 4. 대역 외(Out-of-band) 비상 제어 명령 (E-STOP 긴급 정지)
    ValueTask SendUrgentAsync(TMessage urgentMessage);
    
    ValueTask StartAsync(CancellationToken ct = default);
    ValueTask StopAsync();
}
```

</div>
</div>

<!-- Section 4: Exceptions -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 14px; color: #dc2626;">4. Fail-Fast 예외 계층 체계</span>
<span style="background: #fee2e2; color: #b91c1c; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Exceptions</span>
</div>
<div style="padding: 14px 18px;">

```csharp
namespace Kable.Exceptions;

// 물리 링크 단절 시 모든 대기자에게 즉시 예외 전파 (Fail-Fast)
public class DeviceDisconnectedException : Exception
{
    public DeviceDisconnectedException(string message) : base(message) { }
}

// 장비가 타임아웃 제한 시간 내에 응답하지 않을 때 발생
public class DeviceTimeoutException : TimeoutException
{
    public string Command { get; }
    public TimeSpan Timeout { get; }

    public DeviceTimeoutException(string command, TimeSpan timeout)
        : base($"장비 명령 '{command}'이(가) {timeout.TotalSeconds:F1}초 후 타임아웃되었습니다.")
    {
        Command = command;
        Timeout = timeout;
    }
}
```

</div>
</div>

</div>
