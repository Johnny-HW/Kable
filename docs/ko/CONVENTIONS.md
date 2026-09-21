<!-- Kable Conventions & Strict Rules Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #475569 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(148, 163, 184, 0.18); border: 1px solid rgba(148, 163, 184, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #cbd5e1; margin-bottom: 14px;">
<span>📏 코드 품질 및 아키텍처 거버넌스</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      코딩 컨벤션 및 엄격 규칙 (Conventions & Rules)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable 코드베이스 개발 및 AI 페어 프로그래밍을 위한 코딩 표준, 파일 라인 수 제한 및 Zero-Allocation 가이드라인입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #475569; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">300~500 라인 엄수</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">동기 블로킹 금지 (.Result)</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">핫패스 Zero-GC</span>
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">TDD 테스트 주도 개발</span>
</div>
</div>

<!-- Strict Prohibitions Box -->
<div style="background: #fef2f2; border-left: 4px solid #ef4444; border-radius: 0 8px 8px 0; padding: 16px 20px; margin-bottom: 26px;">
<div style="font-weight: 700; color: #b91c1c; font-size: 13.5px; margin-bottom: 6px;">⛔ 엄격 금지 사항 (CI 빌드 및 PR 실패 조건)</div>
<ul style="margin: 0; padding-left: 18px; font-size: 12.5px; color: #7f1d1d; line-height: 1.6;">
<li><strong>동기 블로킹 절대 금지</strong>: <code>.Result</code>, <code>.Wait()</code>, <code>.GetAwaiter().GetResult()</code> 사용을 일절 금합니다. 모든 I/O는 <code>CancellationToken</code>을 수반한 비동기(Async)로 전파되어야 합니다.</li>
<li><strong>예외 삼키기 금지</strong>: 비어있는 <code>catch { }</code> 블록은 엄격히 금지됩니다. 연결 이상은 지체 없이 Fail-Fast로 전파해야 합니다.</li>
<li><strong>무거운 UI/ORM 패키지 참조 금지</strong>: 코어 <code>Kable</code> 라이브러리는 WPF/WinForms 등의 UI 프레임워크나 무거운 디스크 ORM 패키지를 의존해서는 안 됩니다.</li>
</ul>
</div>

</div>
