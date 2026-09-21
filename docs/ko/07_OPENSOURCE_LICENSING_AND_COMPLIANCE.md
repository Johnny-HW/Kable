<!-- Kable Open-Source Licensing and Compliance Guide (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #047857 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(52, 211, 153, 0.18); border: 1px solid rgba(52, 211, 153, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #34d399; margin-bottom: 14px;">
<span>⚖️ ZERO-COPYLEFT 라이선스 컴플라이언스</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      07. 오픈소스 라이선스 및 컴플라이언스 가이드
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      반도체 및 산업 자동화 장비의 100% 독점 상용 제품 납품을 보장하는 Zero-Copyleft 거버넌스 및 허용적(Permissive) 오픈소스 라이선스(Apache-2.0, MIT, BSD-3-Clause) 규정입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Apache-2.0 프레임워크</span>
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">MIT / BSD 의존성</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero Copyleft (GPL 배제)</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">명시적 특허권 보장</span>
</div>
</div>

<!-- Key Pillars -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">🛡️ Zero-Copyleft 보장</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">GPL, LGPL, AGPL 라이선스 패키지는 엄격히 배제됩니다. 설비 제조사(OEM)는 자사의 독점 공정 레시피 소스 코드를 외부에 공개할 법적 의무가 전혀 없습니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">📜 Permissive(허용적) 오픈소스 전용</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">오직 Apache-2.0, MIT, BSD-3-Clause 및 MS-PL 라이선스 패키지만 Kable 코어 및 공식 확장 패키지에 허용됩니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #d97706;">
<div style="font-weight: 700; color: #b45309; font-size: 14px; margin-bottom: 6px;">🔒 명시적 특허 사용권 부여 (Patent Grant)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Apache-2.0 규정에 의해 기여자(Microsoft, Google 등)로부터 영구적이고 전 세계적인 명시적 특허 실시권을 자동으로 부여받습니다.</div>
</div>
</div>

<!-- Section 1: Dependency License Matrix -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #059669; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. 코어 및 에코시스템 라이선스 매트릭스
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">패키지명</th>
<th style="padding: 12px 14px; font-weight: 700;">역할 / 도메인</th>
<th style="padding: 12px 14px; font-weight: 700;">소유자</th>
<th style="padding: 12px 14px; font-weight: 700;">라이선스</th>
<th style="padding: 12px 14px; font-weight: 700;">소스코드 공개 의무</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">System.IO.Pipelines</td>
<td style="padding: 12px 14px;">0-GC 파이프라인 전송 계층</td>
<td style="padding: 12px 14px;">Microsoft (.NET Foundation)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #059669;">MIT</td>
<td style="padding: 12px 14px; color: #059669;">없음 (안전)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">System.IO.Ports</td>
<td style="padding: 12px 14px;">RS-232 / 485 시리얼 통신</td>
<td style="padding: 12px 14px;">Microsoft (.NET Foundation)</td>
<td style="padding: 12px 14px; font-weight: 600; color: #059669;">MIT</td>
<td style="padding: 12px 14px; color: #059669;">없음 (안전)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">Kable Core Framework</td>
<td style="padding: 12px 14px;">반응형 엔진 및 클라이언트 빌더</td>
<td style="padding: 12px 14px;">Kable Authors</td>
<td style="padding: 12px 14px; font-weight: 600; color: #059669;">Apache-2.0</td>
<td style="padding: 12px 14px; color: #059669;">없음 (안전)</td>
</tr>
</tbody>
</table>
</div>
</div>

</div>
