# 도메인별 파일 분리 완료 보고

작성일: 2026-10-16. [승인된 계획](implementation_plan.md)에 따른 구조 정리다.

## 결과

| 대상 | 변경 전 | 변경 후 |
| --- | --- | --- |
| 산업 신뢰성 테스트 | 882줄, 요청·수명·스트림·스풀·fixture 혼합 | Messaging, Lifecycle, Streaming, Alarms의 6개 테스트 클래스와 연결 fixture 5개 |
| 인터리빙/복구 테스트 | 472줄, 공통 코덱 포함 | Messaging, Lifecycle, Streaming의 4개 테스트 클래스와 Fixtures/Codecs |
| 장시간 부하 테스트 | 547줄, FIFO/CID 혼합 | Soak/FifoSoakTests, MultiplexedSoakTests와 전용 코덱·줄 파서 |
| 전송 장애 테스트 | 449줄, TCP/파이프/시리얼 혼합 | Tcp, NamedPipe, Serial, PipeBackpressure, TelemetryThroughput 테스트 |
| 로컬라이저 | 796줄, 조회 로직과 번역 데이터 혼합 | 127줄 조회 로직과 Translations의 8개 언어별 파일 |
| 세션 수명 | 357줄 | Lifecycle/Start 146줄, Cleanup 178줄, Disconnect 59줄 |
| 세션 스트림 | 343줄 | Streaming/Stream 206줄, InboundBuffer 151줄 |
| 알람 스풀 | Engine 루트 | Engine/Alarms로 파일 이동 |

신규·변경 C# 파일 41개는 모두 300줄 미만이며 최대 266줄이다.
표에 사용한 경로의 제품 루트는 `src/Kable/Engine`,
테스트 루트는 `tests/Kable.Tests/Cases`와 `tests/Kable.Tests/Fixtures`다.
로컬라이저 경로는 `src/Kable.Core/Localization`이다.

## 설계상 유지한 사항

- 제품 namespace와 공개 API, 객체·락·채널의 소유권을 유지했다.
  새 계층·인터페이스·런타임 객체를 도입하지 않았다.
- 이동한 세션 메서드 15개의 선언 및 본문은 원본과 정확히 일치한다.
- 기존 테스트 메서드 54개와 assertion·timeout 본문을 보존했다.
  부하 테스트의 전용 코덱 이름만 `TestCorrelationCodec`에서
  `SoakCorrelationCodec`으로 변경했다.
- HardwareTransportTests 컬렉션을 해당 테스트에 유지했다.
  두 soak 클래스는 명시적으로 선언한 SessionSoakTests 컬렉션으로 묶어
  기존처럼 서로 직렬 실행한다. 다른 테스트 컬렉션의 병렬 설정은 바꾸지 않았다.
- 번역 554개 항목의 키·값과 사전 비교자를 보존했다.
  오류 사전 8개 문화권, UI 사전 7개 문화권과 프랑스어 UI 영어 fallback을 유지했다.
- 기존 테스트 클래스 이름은 책임에 맞게 변경되었으므로 클래스 이름 기반의
  외부 테스트 필터는 새 이름을 사용해야 한다. 메서드 이름은 유지했다.

## 검증 결과

1. 분리 전 핵심 테스트: 288개 통과.
2. 테스트 분리 직후 핵심 테스트: 288개 통과.
3. 번역 분리 후 Localization 테스트: 10개 통과.
4. `dotnet build src/Kable.Core/Kable.Core.csproj --no-restore --verbosity quiet`:
   성공, 경고 0 / 오류 0. netstandard2.0, net8.0, net10.0 대상 포함.
5. `dotnet build Kable.sln --no-restore --verbosity quiet`:
   성공, 경고 0 / 오류 0.
6. `dotnet test Kable.sln --no-build --no-restore -m:1 --verbosity quiet`:
   12개 프로젝트 총 360개 통과 / 실패 0 / 건너뜀 0.
   `-m:1`은 프로젝트 실행을 직렬화하며 테스트 assertion을 완화하지 않는다.
7. 테스트 본문·세션 메서드·번역 항목 비교, 변경 파일 줄 수 및
   `git diff --check`: 통과.

## 중간 실패와 제한

- 중간 핵심 테스트 실행에서 TC_TRN_08 텔레메트리 테스트가 한 번 실패했다.
  단독 재실행과 최종 전체 직렬 검증에서는 통과했다.
  최초 실행의 quiet 출력으로 정확한 예외/실패 assertion은 보존되지 않았으므로
  이를 GC 문제라고 단정하지 않는다.
- 프로젝트 병렬 실행에서는 기존 ProfileClient_GenericEnum_PeriodicPollingAndLatestCache_WorksCorrectly가
  200ms 대기 후 기대한 STATUS:READY 대신 null을 확인하여 실패했다.
  최종 직렬 검증에서는 통과했다. 시간에 민감한 검사로 관찰됐지만
  근본 원인은 이번 구조 정리에서 확정하거나 수정하지 않았다.
- 위 두 테스트의 본문은 변경하지 않았고 테스트 제외·timeout 증가·assertion 완화도 하지 않았다.
  기본 병렬 실행의 반복 안정성을 보장한다고 주장하지 않는다.
- 임시 분리 도구의 검증 단계가 관련 없는 테스트 메서드를 파싱하다 한 번 실패했다.
  인덱스를 HEAD로 복원해 작업 파일을 보존하고 검증 대상을 바로잡았다.
  이후 기존 54개 메서드의 보존 검증을 완료했다.
- 실제 장비 시험이나 성능 개선 측정은 수행하지 않았다.

## 커밋

- `36a4bb4`: 테스트 도메인 분리와 공통 fixture 추출.
- `84025ba`: 언어별 번역 데이터 분리.
- `076eae7`: 세션 수명·스트림 책임 분리와 알람 폴더 정리.
