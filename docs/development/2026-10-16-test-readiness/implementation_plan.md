# 통합 테스트 완료 신호와 진단 보강

작성일: 2026-10-16. 사용자가 설명된 두 테스트의 개선을 승인했다.

## 범위와 기준

- PROJECT_SPEC, SYSTEM_DESIGN, CONVENTIONS 및 연결 수명 계약을 유지한다.
- 제품 동작 변경 없이 테스트의 고정 200ms/100ms 성공 가정을 제거한다.
- 프로파일: 첫 상태·압력 수신 이벤트를 제한시간 내 기다린 뒤 기존 캐시 검사를 수행한다.
- 텔레메트리: 송신 시작을 명시적으로 제어하고 실제 2,000개 수신 완료를 알린 뒤 서버를 닫는다.
- TCP 테스트 서버는 비동기 줄 프레이밍/전체 쓰기를 사용한다.
- 실패 시 서버 단계·송수신 건수·마지막 명령·세션 상태·최근 통신 로그를 출력한다.
- 정상 응답 값, 요청 2초 timeout, 텔레메트리 수신 제한 5초,
  패킷 2,000개 및 Gen2 GC 증가 <= 1 검사는 유지한다.

## 대상 파일 (각 300줄 미만)

- 수정: `tests/Kable.Tests/Cases/Profiles/KableProfileClientTests.cs`
  - partial 선언 후 기존 TCP 폴링 테스트를 별도 파일로 이동한다.
- 추가: `tests/Kable.Tests/Cases/Profiles/KableProfileClientTests.Polling.cs`
  - 이벤트 구독 전 응답을 gate로 막고 구독 후 해제하여 신호 유실을 방지한다.
  - 정상 응답과 첫 응답 300ms 지연 주입을 검증한다.
- 수정: `tests/Kable.Tests/Cases/Transports/TelemetryThroughputTests.cs`
  - 초기 GC/측정 준비 후 송신 시작, 실제 수신 완료 후 연결 종료.
  - 정상 소비와 송신 완료 후 소비자 300ms 지연 주입을 검증한다.
- 추가: `tests/Kable.Tests/Fixtures/Profiles/ProfilePollingServer.cs`
- 추가: `tests/Kable.Tests/Fixtures/Transports/TelemetryTestServer.cs`
- 추가: `tests/Kable.Tests/Infrastructure/TestTrafficDiagnostics.cs`
  - 고정 크기 최근 로그 버퍼로 실패 시만 문자열 진단을 만든다.
- 추가: 이 폴더의 `walkthrough.md`.

## 검증

기존 두 테스트는 작업 전 실행에서 모두 통과했다. 과거 실패를 이번에 다시
재현했다고 주장하지 않는다. 최초 텔레메트리 실패의 정확한 원인은 미확정이다.

지연 주입은 기존 대기 가정(200ms/100ms)을 넘도록 선택한 테스트 입력이다.
지연이 끝났다는 사실을 성공으로 보지 않고 수신 이벤트/개수로 완료를 확인한다.
서버 오류나 조기 단절은 완료 대기와 경쟁시켜 조기에 실패시키고, 정리도 제한시간을 둔다.
이벤트 구독 해제·연결 폐기·서버 취소와 await를 모든 종료 경로에 수행한다.

관련 테스트 반복 실행, 전체 솔루션 병렬 프로젝트 실행 반복과 빌드를 검증한다.
재현되지 않는 과거 원인을 단정하거나 새 실패를 숨기지 않는다.
검증 단위별 커밋 후 실제 결과를 완료 문서에 기록한다.
