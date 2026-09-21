<!-- Kable Implementation Layout Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📦 패키징 및 디렉터리 체계 (PACKAGING & TAXONOMY)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      04. 구현 및 디렉터리 구조 (Implementation Layout)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      저장소 디렉터리 체계, 네임스페이스 분할 규칙, <code>Kable</code> 및 <code>Kable.Core</code> 독립형 NuGet 배포 구조입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Kable.nupkg</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Kable.Core.nupkg</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Roslyn 소스 생성기</span>
</div>
</div>

<!-- Repository Tree -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      📁 독립형 리포지토리 파일 트리
</div>
<div style="padding: 14px 18px;">

```
Kable/                                         # [저장소 루트]
│
├── .github/workflows/
│   ├── ci-cd.yml                              # 자동 빌드, 단위 테스트 및 NuGet 패키지 배포 파이프라인
│   └── pages.yml                              # GitHub Pages Docsify 정적 문서 사이트 배포
│
├── Kable.sln                                  # 단독 솔루션 파일
├── README.md                                  # 라이브러리 메인 영문 설명서 및 빠른 시작
├── README.ko.md                               # 라이브러리 메인 한국어 개발자 가이드
├── AGENTS.md                                  # AI 에이전트 작업 헌장 및 아키텍처 불변식
├── CONTRIBUTING.md                            # 기여 가이드라인
├── CHANGELOG.md                               # 버전 릴리즈 이력 (SemVer 표준)
│
├── docs/                                      # [Docsify 기반 정적 문서 사이트 루트]
│   ├── index.html                             # Docsify 싱글 페이지 애플리케이션 진입점 (i18n)
│   ├── _navbar.md                             # 전역 언어 전환 상단 바 (Language Switcher)
│   │
│   ├── en/                                    # [English 영문 문서 디렉터리]
│   │   ├── _sidebar.md                        # 영문 내비게이션 사이드바
│   │   ├── _navbar.md                         # 영문 상단 바
│   │   ├── README.md                          # 영문 메인 퀵스타트
│   │   └── *.md                               # 01~07 기술 사양서
│   │
│   └── ko/                                    # [한국어 문서 디렉터리]
│       ├── _sidebar.md                        # 한국어 내비게이션 사이드바
│       ├── _navbar.md                         # 한국어 상단 바
│       ├── README.md                          # 한국어 메인 퀵스타트
│       └── *.md                               # 01~07 기술 사양서 번역본
│       └── *.md                               # 한국어 번역 사양서
│
├── src/
│   ├── Kable.Core/                            # [순수 추상화 계약 계층]
│   │   ├── IConnectionContext.cs              # Bedrock Pipelines 입출력 계약
│   │   ├── IProtocolCodec.cs                  # Zero-allocation 프레이밍 계약
│   │   └── Checksums/                         # CRC-16, LRC, XOR BCC 고속 알고리즘
│   │
│   └── Kable/                                 # [엔진 구현체 및 어댑터 계층]
│       ├── Transports/                        # TCP, SerialPort, NamedPipe
│       ├── Engine/                            # KableSession (FIFO 락 + Lock-Free 라우터)
│       ├── Observability/                     # 3채널 링버퍼 (ICommObserver)
│       └── Extensions/                        # KableClientBuilder 및 DI 확장 메서드
│
└── tests/
    └── Kable.Tests/                           # 단위 테스트, 결함 주입, 계약 검증 테스트 슈트
```

</div>
</div>

</div>
