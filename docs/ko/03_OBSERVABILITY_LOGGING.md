<!-- Kable Observability & Logging Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #4338ca 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(129, 140, 248, 0.18); border: 1px solid rgba(129, 140, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #a5b4fc; margin-bottom: 14px;">
<span>📊 3채널 링버퍼 관측성 (TRI-STREAM RINGBUFFER OBSERVABILITY)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
03. 관측성 및 로깅 명세서 (Observability & Logging)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
주기적 텔레메트리, 커맨드 콘솔 트래픽, 자발적 알람을 3개 채널로 물리 격리하여 <strong>UI 멈춤 현상을 원천 방지하고 감사 추적(Audit Trail) 규제 준수를 보장</strong>합니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #4f46e5; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">DropOldest 링버퍼</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">60 FPS UI 렌더링 보장</span>
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">핫패스 디스크 I/O 배제</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">FDA 21 CFR Part 11 Audit Trail</span>
</div>
</div>

<!-- 3-Pillar Architecture Grid -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">⚡ 코어 엔진 (Kable Core)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">오직 0-GC 패킷 라우팅에만 집중하며, 디스크 파일 I/O 및 데이터베이스 삽입 작업은 코어 파이프라인에서 100% 배제됩니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">📁 주입된 로거 (Serilog 등)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">규제 준수를 위한 무손실 아카이빙은 백그라운드 워커가 날짜별 롤링 파일(Daily Rolling File)을 통해 비동기 처리합니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #4f46e5;">
<div style="font-weight: 700; color: #4338ca; font-size: 14px; margin-bottom: 6px;">🖥️ UI 모니터링 (WPF/Web)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;"><code>BoundedChannelFullMode.DropOldest</code>를 강제 적용하여 100Hz 초고속 스트리밍 중에도 UI 디스패처가 멈추지 않고 60 FPS 렌더링을 유지합니다.</div>
</div>
</div>

<!-- Traffic Channels Comparison Table -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
1. 3채널 트래픽 분류 체계 (TrafficKind)
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">분류 (Kind)</th>
<th style="padding: 12px 14px; font-weight: 700;">트래픽 성격</th>
<th style="padding: 12px 14px; font-weight: 700;">버퍼 정책</th>
<th style="padding: 12px 14px; font-weight: 700;">주요 바인딩 대상</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">AperiodicCommand (비주기 커맨드)</td>
<td style="padding: 12px 14px; color: #334155;">시퀀스 실행, 서보 모터 제어, 사용자 트리거 트랜잭션 질의 및 응답</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#e0f2fe; color:#0369a1; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">무손실 영구 보관 (Lossless)</span></td>
<td style="padding: 12px 14px; color: #334155;">HMI 커맨드 콘솔, 감사 추적(Audit Trail) 로그</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #059669;">PeriodicTelemetry (주기적 텔레메트리)</td>
<td style="padding: 12px 14px; color: #334155;">10~100Hz 고속 센서 데이터, 온도/압력 폴링, 하트비트 핑퐁</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#dcfce7; color:#15803d; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">DropOldest 링버퍼</span></td>
<td style="padding: 12px 14px; color: #334155;">실시간 차트, 게이지, 트렌드 시각화 컨트롤</td>
</tr>
<tr style="background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #dc2626;">SpontaneousAlarm (자발적 알람)</td>
<td style="padding: 12px 14px; color: #334155;">장비 자발적 결함 통보, 안전 인터록 트립, 비상 정지(E-STOP)</td>
<td style="padding: 12px 14px; color: #475569;"><span style="background:#fee2e2; color:#b91c1c; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">우선순위 무손실 큐</span></td>
<td style="padding: 12px 14px; color: #334155;">알람 팝업 창, 이상 이력 데이터베이스</td>
</tr>
</tbody>
</table>
</div>
</div>

<!-- Section 2: Code Specification -->
<div style="margin-bottom: 30px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
2. ICommObserver 코어 인터페이스 규약
</h2>

```csharp
namespace Kable.Observability;

using System;
using System.Threading.Channels;

public interface ICommObserver
{
    // [엔진 -> 옵저버 0-GC 빠른 디스패치] 핫패스 상에서 블로킹 없이 즉시 반환
    void OnPacketTrace(in PacketTraceRecord trace);

    // [UI 전용 격리 채널: DropOldest 링버퍼 정책 적용]
    ChannelReader<PacketTraceRecord> PeriodicStream { get; }  // 실시간 차트/게이지 바인딩
    ChannelReader<PacketTraceRecord> CommandStream  { get; }  // 커맨드 콘솔 바인딩
    ChannelReader<PacketTraceRecord> AlarmStream    { get; }  // 알람 팝업 및 이력 바인딩
}
```

</div>

<!-- Note Box -->
<div style="background: #f0f9ff; border-left: 4px solid #0284c7; border-radius: 0 8px 8px 0; padding: 14px 18px; margin-top: 24px;">
<div style="font-weight: 700; color: #0369a1; font-size: 13px; margin-bottom: 4px;">💡 실전 아키텍처 가이드</div>
<div style="font-size: 12.5px; color: #334155; line-height: 1.6;">
WPF 또는 WinForms와 같은 단일 UI 스레드 환경에서 초고주기 텔레메트리를 콘솔 UI로 직접 파이핑하면 디스패처 루프가 즉각 동결됩니다. Kable의 <code>ICommObserver</code>는 <strong>PeriodicStream (DropOldest)</strong>과 <strong>CommandStream (무손실 보관)</strong>을 물리적으로 분리하여, 설비의 고속 통신 중에도 화면이 멈추지 않는 60 FPS 렌더링을 영구 보장합니다.
</div>
</div>

</div>
