<!-- Kable Industrial High-Reliability Communication Roadmap Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🌐 프로토콜 스펙트럼 및 연동 로드맵 (PROTOCOL SPECTRUM & ROADMAP)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      06. 산업용 고신뢰성 통신 로드맵 (High-Reliability Roadmap)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      산업 현장의 13대 주요 통신 프로토콜에 대한 정형 기술 비교, 결정론(Determinism) 등급 분류 및 Kable 공식 통합 경계 규약입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">13종 프로토콜 스펙트럼</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Soft vs Hard 실시간</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IPC 메모리 맵 파일(MMF)</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">OPC UA & MQTTnet</span>
</div>
</div>

<!-- Scope Notice Box -->
<div style="background: #eff6ff; border-left: 4px solid #3b82f6; border-radius: 0 8px 8px 0; padding: 16px 20px; margin-bottom: 26px;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 13.5px; margin-bottom: 4px;">📌 시스템 경계 및 개발 스코프 정의</div>
<div style="font-size: 12.5px; color: #334155; line-height: 1.6;">
<strong>Kable은 하드 실시간 모션 제어기나 공인 안전(Safety) 제어기가 아닙니다.</strong><br/>
      Kable은 <strong>비안전 장비 통신, 제어 관측성 및 고속 텔레메트리 스트리밍</strong>을 위한 초고성능 Zero-Allocation 중계 엔진입니다. 서브 밀리초 단위의 정밀 모션 필드버스(EtherCAT, CIP Safety 등)는 검증된 외부 마스터 SDK 브리지를 통해 상호 연동합니다.
</div>
</div>

<!-- Section 1: Protocols Comparison Matrix -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. 산업용 통신 프로토콜 비교 매트릭스
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">도메인</th>
<th style="padding: 12px 14px; font-weight: 700;">프로토콜 / 기술</th>
<th style="padding: 12px 14px; font-weight: 700;">결정론 등급</th>
<th style="padding: 12px 14px; font-weight: 700;">라이선스</th>
<th style="padding: 12px 14px; font-weight: 700;">Kable 지원 현황</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">IPC (단일 OS)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Named Pipe IPC</td>
<td style="padding: 12px 14px;">소프트 실시간 (Soft RT)</td>
<td style="padding: 12px 14px;">OS 내장 (무료)</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ 내장 기본 지원 (UseNamedPipe)</span></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">IPC (단일 OS)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">MMF 공유큐 / 링버퍼</td>
<td style="padding: 12px 14px;">소프트 실시간 (Soft RT)</td>
<td style="padding: 12px 14px;">Apache-2.0</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.SharedMemory</span></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">네트워크 (원격)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Raw TCP Socket (NoDelay)</td>
<td style="padding: 12px 14px;">Best Effort</td>
<td style="padding: 12px 14px;">OS 내장 (무료)</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ 내장 기본 지원 (UseTcp)</span></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">네트워크 (원격)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">gRPC (HTTP/2 + Protobuf)</td>
<td style="padding: 12px 14px;">소프트 실시간 (Soft RT)</td>
<td style="padding: 12px 14px;">Apache-2.0</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.Grpc</span></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">필드버스 / PLC</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">RS-232C / RS-485 시리얼</td>
<td style="padding: 12px 14px;">소프트 실시간 (Soft RT)</td>
<td style="padding: 12px 14px;">OS 내장 (무료)</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ 내장 기본 지원 (UseSerialPort)</span></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">필드버스 / PLC</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Modbus-TCP / RTU</td>
<td style="padding: 12px 14px;">소프트 실시간 (Soft RT)</td>
<td style="padding: 12px 14px;">Apache-2.0</td>
<td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.Modbus</span></td>
</tr>
<tr style="background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #475569;">필드버스 / PLC</td>
<td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">EtherCAT</td>
<td style="padding: 12px 14px;">하드 실시간 (Hard RT)</td>
<td style="padding: 12px 14px;">상용 라이선스 / GPL</td>
<td style="padding: 12px 14px;"><span style="background:#fee2e2; color:#b91c1c; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">🚫 Kable 코어 패키지 제외 (외부 브리지)</span></td>
</tr>
</tbody>
</table>
</div>
</div>

</div>
