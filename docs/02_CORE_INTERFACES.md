<!-- Kable Core Interfaces Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
      <span>⚙️ CONTRACT SPECIFICATION</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      02. Core Interfaces Specification
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Formal API contracts for lower Bedrock transport contexts, middle zero-allocation codecs, and upper reactive RSocket sessions.
    </p>
    <div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
      <span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IConnectionContext</span>
      <span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IProtocolCodec&lt;T&gt;</span>
      <span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IDeviceSession&lt;T&gt;</span>
      <span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast Exceptions</span>
    </div>
  </div>

  <!-- Contract 1: IConnectionContext -->
  <div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 22px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
    <div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
      <span style="font-weight: 700; font-size: 14px; color: #0369a1;">1. Lower Transport Abstraction (IConnectionContext)</span>
      <span style="background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Core</span>
    </div>
    <div style="padding: 14px 18px;">
      <p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Full-duplex zero-allocation pipelines context representing physical (TCP, Serial) and logical (Named Pipe) channels:
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
    
    // Bedrock Standard 0-GC Pipelines
    PipeReader Input { get; }
    PipeWriter Output { get; }
    
    // Disconnection Notification Token
    CancellationToken ConnectionClosed { get; }
    
    void Abort(string reason);
}
```

    </div>
  </div>

  <!-- Contract 2: IProtocolCodec -->
  <div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 22px; overflow: hidden; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.02);">
    <div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; display: flex; justify-content: space-between; align-items: center;">
      <span style="font-weight: 700; font-size: 14px; color: #059669;">2. Zero-Allocation Framing Codec (IProtocolCodec&lt;TMessage&gt;)</span>
      <span style="background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Codecs</span>
    </div>
    <div style="padding: 14px 18px;">
      <p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Transforms wire byte sequences into domain messages with zero intermediate buffer copies:
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
      <span style="font-weight: 700; font-size: 14px; color: #6366f1;">3. Reactive Upper Session (IDeviceSession&lt;TMessage&gt;)</span>
      <span style="background: #ede9fe; color: #6d28d9; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Engine</span>
    </div>
    <div style="padding: 14px 18px;">
      <p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Standard public-facing facade for industrial equipment control:
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
    
    // 1. Real-time telemetry streaming channel
    IAsyncEnumerable<TMessage> Stream { get; }
    
    // 2. Fire-and-forget notification
    ValueTask SendAsync(TMessage message, CancellationToken ct = default);
    
    // 3. Request-Response RPC with Watchdog isolation
    ValueTask<TResponse> RequestAsync<TResponse>(TMessage request, TimeSpan timeout, CancellationToken ct = default);
    
    // 4. Out-of-band urgent emergency command (E-STOP)
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
      <span style="font-weight: 700; font-size: 14px; color: #dc2626;">4. Fail-Fast Exception Hierarchy</span>
      <span style="background: #fee2e2; color: #b91c1c; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Kable.Exceptions</span>
    </div>
    <div style="padding: 14px 18px;">

```csharp
namespace Kable.Exceptions;

// Dispatched immediately to all pending callers upon physical link termination (Fail-Fast)
public class DeviceDisconnectedException : Exception
{
    public DeviceDisconnectedException(string message) : base(message) { }
}

// Dispatched when an instrument fails to respond within the deadline
public class DeviceTimeoutException : TimeoutException
{
    public string Command { get; }
    public TimeSpan Timeout { get; }

    public DeviceTimeoutException(string command, TimeSpan timeout)
        : base($"Device command '{command}' timed out after {timeout.TotalSeconds:F1}s.")
    {
        Command = command;
        Timeout = timeout;
    }
}
```

    </div>
  </div>

</div>
