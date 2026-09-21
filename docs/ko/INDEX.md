<!-- Kable Master Documentation Index (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 기술 문서 종합 색인 (MASTER DOCUMENTATION INDEX)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 마스터 인덱스 (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable 반응형 하드웨어 통신 엔진의 전체 기술 사양서 목차 및 탐색 맵입니다.
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">분류</th>
<th style="padding: 12px 16px; font-weight: 700;">문서 제목</th>
<th style="padding: 12px 16px; font-weight: 700;">주요 다룸 내용</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">시작하기 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/README" style="color: #2563eb; text-decoration: none;">개요 및 퀵스타트 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet 패키지, KableClientBuilder, KableSimple 퍼사드, DI 컨테이너 연동</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">시작하기 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">프로젝트 사양서 (Project Spec)</a></td>
<td style="padding: 12px 16px; color: #475569;">단일 진실 공급원(SSOT), 4대 핵심 아키텍처 결정 및 불변식</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">아키텍처 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. 아키텍처 개요 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3계층 구조 및 통합 클래스 다이어그램</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">아키텍처 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. 코어 인터페이스 명세서</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> 계약</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">아키텍처 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. 구현 및 디렉터리 구조</a></td>
<td style="padding: 12px 16px; color: #475569;">리포지토리 디렉터리 트리, 네임스페이스 가이드 및 패키징 구조</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">신뢰성 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. 관측성 및 로깅 (Observability)</a></td>
<td style="padding: 12px 16px; color: #475569;">3채널 유한 링버퍼(<code>DropOldest</code>) 및 60 FPS UI 렌더링 보장</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">신뢰성 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. 산업용 체크섬 (Checksums)</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, XOR BCC 0-GC 룩업 테이블 알고리즘</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">신뢰성 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. 산업용 통신 로드맵</a></td>
<td style="padding: 12px 16px; color: #475569;">13대 산업용 프로토콜 스펙트럼, 결정론 등급 및 로드맵</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">거버넌스 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/CONVENTIONS" style="color: #d97706; text-decoration: none;">코딩 컨벤션 (Conventions)</a></td>
<td style="padding: 12px 16px; color: #475569;">파일 라인 수 제한(300~500), 동기 블로킹 금지 및 TDD 요구사항</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">거버넌스 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ko/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. 오픈소스 라이선스 가이드</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 보장, 허용적 라이선스 매트릭스(Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>
