<!-- Kable Observability & Logging Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #4338ca 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(129, 140, 248, 0.18); border: 1px solid rgba(129, 140, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #a5b4fc; margin-bottom: 14px;">
<span>📊 TRI-STREAM RINGBUFFER OBSERVABILITY</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
03. Observability & Logging Specification
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
Isolates periodic telemetry, command console traffic, and spontaneous alarms to <strong>completely eliminate UI freezing and guarantee regulatory compliance (Audit Trail)</strong>.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #4f46e5; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">DropOldest Ringbuffer</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">60 FPS UI Guaranteed</span>
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero Disk I/O in Hotpath</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">FDA 21 CFR Part 11 Audit Trail</span>
</div>
</div>

<!-- 3-Pillar Architecture Grid -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">⚡ Core Engine (Kable)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Focuses exclusively on 0-GC packet routing; disk I/O and DB insertions are 100% excluded from the core pipeline.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">📁 Injected Loggers (Serilog)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Lossless regulatory compliance archival is handled asynchronously by background workers via daily rolling files.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #4f46e5;">
<div style="font-weight: 700; color: #4338ca; font-size: 14px; margin-bottom: 6px;">🖥️ UI Monitoring (WPF/Web)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Enforces <code>BoundedChannelFullMode.DropOldest</code> to guarantee stutter-free 60 FPS rendering even under 100Hz streaming.</div>
</div>
</div>

<!-- Traffic Channels Comparison Table -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
1. Tri-Channel Traffic Classification (TrafficKind)
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">Classification (Kind)</th>
<th style="padding: 12px 14px; font-weight: 700;">Traffic Nature</th>
<th style="padding: 12px 14px; font-weight: 700;">Buffer Policy</th>
<th style="padding: 12px 14px; font-weight: 700;">Primary Binding Target</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">AperiodicCommand</td>
<td style="padding: 12px 14px; color: #334155;">Sequence execution, servo control, user-triggered transaction queries & replies</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#e0f2fe; color:#0369a1; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">Lossless Archival</span></td>
<td style="padding: 12px 14px; color: #334155;">HMI Command Console, Audit Trail logs</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #059669;">PeriodicTelemetry</td>
<td style="padding: 12px 14px; color: #334155;">10~100Hz high-speed sensors, temperature/pressure polling, heartbeat ping-pong</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#dcfce7; color:#15803d; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">DropOldest Ringbuffer</span></td>
<td style="padding: 12px 14px; color: #334155;">Real-time charts, gauges, trend visualizations</td>
</tr>
<tr style="background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #dc2626;">SpontaneousAlarm</td>
<td style="padding: 12px 14px; color: #334155;">Device unsolicited faults, safety interlock trips, emergency stops (E-STOP)</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#fee2e2; color:#b91c1c; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">Priority Lossless Queue</span></td>
<td style="padding: 12px 14px; color: #334155;">Alarm popup dialogs, fault history database</td>
</tr>
</tbody>
</table>
</div>
</div>

<!-- Section 2: Code Specification -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
2. ICommObserver Core Interface Specification
</h2>

```csharp
namespace Kable.Observability;

using System;
using System.Threading.Channels;

public interface ICommObserver
{
    // [Engine -> Observer 0-GC Fast Dispatch] Non-blocking return on the hotpath
    void OnPacketTrace(in PacketTraceRecord trace);

    // [UI Dedicated Decoupled Channels with DropOldest Ringbuffers]
    ChannelReader<PacketTraceRecord> PeriodicStream { get; }  // Binds to real-time charts/gauges
    ChannelReader<PacketTraceRecord> CommandStream  { get; }  // Binds to command console
    ChannelReader<PacketTraceRecord> AlarmStream    { get; }  // Binds to alert popups and history
}
```

</div>

<!-- Note Box -->
<div style="background: #f0f9ff; border-left: 4px solid #0284c7; border-radius: 0 8px 8px 0; padding: 14px 18px; margin-top: 24px;">
<div style="font-weight: 700; color: #0369a1; font-size: 13px; margin-bottom: 4px;">💡 Practical Architecture Tip</div>
<div style="font-size: 12.5px; color: #334155; line-height: 1.6;">
In single-threaded UI runtimes like WPF or WinForms, piping high-frequency telemetry directly into a console UI freezes the dispatcher loop. Kable's <code>ICommObserver</code> isolates <strong>PeriodicStream (DropOldest)</strong> from <strong>CommandStream (Lossless)</strong>, permanently guaranteeing smooth 60 FPS UI rendering.
</div>
</div>

</div>
