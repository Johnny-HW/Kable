<!-- Kable Observability & Logging Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Noto Sans KR', Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #4338ca 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(129, 140, 248, 0.18); border: 1px solid rgba(129, 140, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #a5b4fc; margin-bottom: 14px;">
      <span>📊 TRI-STREAM RINGBUFFER OBSERVABILITY</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      03. Observability & Logging Specification
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      주기적 센서 텔레메트리와 비주기 명령 콘솔, 설비 알람 트래픽을 분리하여 <strong>UI 프리징을 원천 차단하고 규정 준수(Audit Trail) 로깅</strong>을 달성하는 관측성 명세서입니다.
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
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">순수 0-GC 패킷 입출력에만 집중하며, 디스크 쓰기나 DB 삽입을 코어 파이프라인에서 100% 배제합니다.</div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
      <div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">📁 Injected Loggers (Serilog)</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">규제 준수용 무손실 아카이빙은 백그라운드 워커에서 일자별 롤링 파일로 비동기 안전 저장됩니다.</div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #4f46e5;">
      <div style="font-weight: 700; color: #4338ca; font-size: 14px; margin-bottom: 6px;">🖥️ UI Monitoring (WPF/Web)</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">100Hz 고속 스트리밍 중에도 <code>BoundedChannelFullMode.DropOldest</code>를 적용하여 UI 버벅임을 방지합니다.</div>
    </div>
  </div>

  <!-- Traffic Channels Comparison Table -->
  <div style="margin-bottom: 30px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. 트래픽 3대 채널 분류 체계 (TrafficKind)
    </h2>

    <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px;">
      <table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
        <thead>
          <tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
            <th style="padding: 12px 14px; font-weight: 700;">분류 (Kind)</th>
            <th style="padding: 12px 14px; font-weight: 700;">트래픽 성격</th>
            <th style="padding: 12px 14px; font-weight: 700;">버퍼 정책</th>
            <th style="padding: 12px 14px; font-weight: 700;">주요 바인딩 타겟</th>
          </tr>
        </thead>
        <tbody>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">AperiodicCommand</td>
            <td style="padding: 12px 14px; color: #334155;">시퀀스 실행, 서보 제어, 사용자가 트리거한 트랜잭션 질의/응답</td>
            <td style="padding: 12px 14px; color: #475569;"><span style="background:#e0f2fe; color:#0369a1; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">무손실 보존 (Lossless)</span></td>
            <td style="padding: 12px 14px; color: #334155;">HMI 커맨드 콘솔, 감사 추적 로그</td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
            <td style="padding: 12px 14px; font-weight: 700; color: #059669;">PeriodicTelemetry</td>
            <td style="padding: 12px 14px; color: #334155;">10~100Hz 고속 센서값, 온도/압력 폴링, 하트비트 핑퐁</td>
            <td style="padding: 12px 14px; color: #475569;"><span style="background:#dcfce7; color:#15803d; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">DropOldest 링버퍼</span></td>
            <td style="padding: 12px 14px; color: #334155;">실시간 차트, 게이지, 트렌드 시각화</td>
          </tr>
          <tr style="background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #dc2626;">SpontaneousAlarm</td>
            <td style="padding: 12px 14px; color: #334155;">장비 자발적 에러, 인터록 해제 알람, 비상 정지(E-STOP)</td>
            <td style="padding: 12px 14px; color: #475569;"><span style="background:#fee2e2; color:#b91c1c; padding:2px 6px; border-radius:4px; font-size:11.5px; font-weight:600;">최우선 무손실 큐</span></td>
            <td style="padding: 12px 14px; color: #334155;">경보 팝업창, 알람 이력 데이터베이스</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

  <!-- Section 2: Code Specification -->
  <div style="margin-bottom: 30px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #4f46e5; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      2. ICommObserver 코어 인터페이스 사양
    </h2>

```csharp
namespace Kable.Observability;

using System;
using System.Threading.Channels;

public interface ICommObserver
{
    // [엔진 -> 관측자 0-GC 고속 전달] 핫패스에서 락 없이 즉시 복귀
    void OnPacketTrace(in PacketTraceRecord trace);

    // [UI 전용 분리 채널 (DropOldest 링버퍼 적용)]
    ChannelReader<PacketTraceRecord> PeriodicStream { get; }  // 센서 게이지/차트 바인딩
    ChannelReader<PacketTraceRecord> CommandStream  { get; }  // 커맨드 콘솔 바인딩
    ChannelReader<PacketTraceRecord> AlarmStream    { get; }  // 경보 팝업/이력 바인딩
}
```

  </div>

  <!-- Note Box -->
  <div style="background: #f0f9ff; border-left: 4px solid #0284c7; border-radius: 0 8px 8px 0; padding: 14px 18px; margin-top: 24px;">
    <div style="font-weight: 700; color: #0369a1; font-size: 13px; margin-bottom: 4px;">💡 실전 아키텍처 팁</div>
    <div style="font-size: 12.5px; color: #334155; line-height: 1.6;">
      WPF나 WinForms 같은 단일 UI 스레드 기반 환경에서 대용량 텔레메트리를 콘솔 창에 전부 출력하려고 하면 메시지 루프가 지연되어 UI 랙이 발생합니다.
      Kable의 <code>ICommObserver</code>는 <strong>PeriodicStream(DropOldest)</strong>과 <strong>CommandStream(무손실)</strong>을 분리하여 60 FPS 화면 렌더링을 영구히 보장합니다.
    </div>
  </div>

</div>
