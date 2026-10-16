# 도메인별 파일 분리 계획

작성일: 2026-10-16. 사용자가 직전 검토의 분할 범위를 승인했다.

## 원칙

- SSOT: PROJECT_SPEC, SYSTEM_DESIGN, CONVENTIONS 및 연결된 한국어 명세.
- 공개 API, namespace, 세션 상태·락 소유권과 메서드 본문을 보존한다.
- 순수 구조 정리이므로 기존 테스트를 회귀 기준으로 사용한다.
- 새 기능·결함 수정·패키지 변경은 포함하지 않는다.
- 변경하는 파일은 300줄 미만으로 유지하고 단계별 빌드·테스트 후 커밋한다.

## 1. 테스트 도메인 및 fixture

분할 대상:
- `tests/Kable.Tests/Cases/Engine/SessionIndustrialReliabilityTests.cs`
- `tests/Kable.Tests/Cases/Engine/SessionInterleavingAndResilienceTests.cs`
- `tests/Kable.Tests/Cases/Engine/LongRunningSoakStressTests.cs`
- `tests/Kable.Tests/Cases/Transports/TransportFaultInjectionTests.cs`

새 파일 그룹:
- `Cases/Engine/Lifecycle/`: 시작·종료 경쟁과 실패 전파 테스트.
- `Cases/Engine/Messaging/`: FIFO, CID, 긴급 송신과 요청 취소 테스트.
- `Cases/Engine/Streaming/`: 스트림 소비와 알람 분리 테스트.
- `Cases/Engine/Alarms/`: 큐 포화, 스풀 저장, 종료 배출 및 집계 테스트.
- `Cases/Engine/Soak/`: FIFO/CID 부하 테스트와 동일 컬렉션 선언.
- `Cases/Transports/`: TcpFaultInjectionTests, NamedPipeFaultInjectionTests,
  SerialFaultInjectionTests, PipeBackpressureTests, TelemetryThroughputTests.
- `Fixtures/Codecs/`: CorrelationIdLineCodec, SoakCorrelationCodec.
- `Fixtures/Engine/`: 제어 가능한 연결 팩토리와 종료 횟수 추적 fixture.
- `Fixtures/Protocols/`: 부하 테스트용 ASCII 줄 읽기 도우미.

테스트 메서드·assertion·timeout은 보존한다. HardwareTransportTests 컬렉션을
분할한 모든 기존 해당 테스트에 유지한다. 두 soak 테스트는 하나의 명시적인
컬렉션으로 묶어 클래스 분리로 서로 병렬 실행되는 변화를 막는다.

## 2. 로컬라이저

- `src/Kable.Core/Localization/KableLocalizer.cs`: 조회·등록·문화권 선택 유지.
- `src/Kable.Core/Localization/Translations/KableLocalizer.<언어>.cs`:
  기존 8개 언어별 오류·UI 사전 초기화. private partial 메서드로 분리한다.
- 기존 키, 값, 비교자, 프랑스어 UI의 영어 fallback을 그대로 유지한다.
- 변경 전후 번역 키/값 목록을 비교하고 기존 Localization 테스트를 실행한다.

## 3. 세션 책임

- `src/Kable/Engine/Lifecycle/KableSession.Start.cs`: 기존 시작 상태와 StartAsync.
- `src/Kable/Engine/Lifecycle/KableSession.Cleanup.cs`: Stop, 정리, Dispose.
- `src/Kable/Engine/Lifecycle/KableSession.Disconnect.cs`: 단절 전파.
- `src/Kable/Engine/Streaming/KableSession.Stream.cs`: 소비자 열거.
- `src/Kable/Engine/Streaming/KableSession.InboundBuffer.cs`: 큐·초과 정책.
- `src/Kable/Engine/Alarms/KableSession.AlarmSpool.cs`: 기존 워커 파일 이동.

기존 lifecycle/stream 파일을 위 파일로 대체한다. 새로운 객체·동기화·할당을
도입하지 않고 동일 partial 클래스의 기존 멤버를 이동한다.

## 완료 기준

기존 테스트 메서드와 본문 보존, 번역 데이터 보존, 이동한 제품 메서드 본문 보존,
전체 솔루션 빌드·테스트 통과, 변경 파일 300줄 미만, clean working tree.
실제 파일 목록과 검증 결과는 `walkthrough.md`에 기록한다.
