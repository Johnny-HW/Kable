<!-- Kable Project Specification SSOT Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 단일 진실 공급원 (SINGLE SOURCE OF TRUTH - SSOT)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      프로젝트 사양서 (Project Specification)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable 반응형 하드웨어 통신 엔진의 사명(Mission), 아키텍처 토폴로지 및 엔지니어링 핵심 불변식입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Bedrock Pipelines</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">RSocket Reactive</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">멀티 타기팅 (Multi-Targeting)</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast 안전성</span>
</div>
</div>

<!-- 4 Core Decisions -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">1. 하이브리드 라우팅</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">연관 ID가 없는 레거시 ASCII 장비는 선점형 FIFO 락으로 자동 직렬화하고, 최신 토큰 프로토콜은 Lock-Free 다중화를 수행합니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 14px; margin-bottom: 6px;">2. Fail-Fast 안전성</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">물리 통신선 단절 시 <code>DeviceDisconnectedException</code>을 즉시 전파하여 설비를 지체 없이 물리적 안전 상태로 전이합니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">3. 엄격한 관심사 분리</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">코어 엔진은 0-GC 패킷 라우팅에 전념하며, 파일 디스크 로깅과 UI 렌더링은 유한 링버퍼로 완전히 분리합니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 14px; margin-bottom: 6px;">4. 순수 멀티 타기팅</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">최신 .NET 10.0, .NET 8.0 LTS, 레거시 및 .NET Framework 4.8과 호환되는 netstandard2.0을 네이티브 컴파일합니다.</div>
</div>
</div>

</div>
