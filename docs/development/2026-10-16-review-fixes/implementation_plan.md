# 내부 검토 3건 수정 계획

작성일: 2026-10-16. 사용자 요청으로 아래 3건의 문서화와 개발이 승인되었다.

## 기준과 범위

- `docs/PROJECT_SPEC.md`, `docs/SYSTEM_DESIGN.md`, `docs/CONVENTIONS.md`와
  연결된 한국어 명세 및 연결 수명 계약을 따른다.
- 공개 API와 Transport → Codec → Engine 경계를 유지한다.
- FIFO 송신 후 취소/타임아웃의 연결 종료, CID 타임아웃의 세션 유지 계약을 유지한다.
- 정상 처리 경로에 추가 배열, 문자열, 타이머를 도입하지 않는다.

## 검토 정정

단일 리더 비한정 채널은 중복 `WaitToReadAsync` 등록 시 이전 대기를 취소한다.
직접 실험에서 100회 등록 후 미완료 1개, 취소 99개를 확인했다.
따라서 두 번째 항목의 무한 대기자 누적 진단은 정정한다.
실제 수정 목적은 불필요한 대기 생성·취소 및 관찰되지 않은 작업을 줄이는 것이다.

## 실행 순서와 대상 파일

각 문서·코드 파일은 300줄 미만으로 유지한다. 기존 536줄 루프는 관련 책임별로 분리한다.

1. Modbus 응답 검증
   - 수정: `src/Kable.Modbus/ModbusTcpMaster.cs`
   - 추가: `tests/Kable.Modbus.Tests/ModbusTcpMasterResponseValidationTests.cs`
   - FC02/03/04의 요청 개수, 응답 byte count, 실제 payload 길이를 비교한다.
   - 잘린 데이터, 초과 데이터, 홀수 레지스터 바이트 수를 예외로 거부한다.
2. 송신 대기 재사용
   - 수정: `src/Kable/Engine/KableSession.Loops.cs`
   - 추가: `src/Kable/Engine/KableSession.Outbound.cs`
   - 추가: `src/Kable/Engine/KableSession.AlarmSpool.cs` (기존 워커 이동)
   - 추가: `tests/Kable.Tests/Cases/Engine/OutboundWaitLifecycleTests.cs`
   - 큐별 미완료 대기 한 개를 보관하고 종료 시 모든 대기를 관찰한다.
   - 일반/긴급 송신, 우선순위와 세션 종료 동작을 보존한다.
3. CID 등록 예외 안전성
   - 수정: `src/Kable/Engine/KableSession.Messaging.cs`
   - 추가: `tests/Kable.Tests/Cases/Engine/CidTimeoutValidationTests.cs`
   - timeout 토큰과 연결 토큰을 CID 등록 전에 생성한다.
   - 잘못된 timeout 후 같은 CID로 정상 요청이 성공하는지 확인한다.

## 검증과 커밋

- 테스트를 먼저 작성하고 기존 코드에서 결함/불필요한 대기 교체를 재현한다.
- 각 수정 후 해당 프로젝트 테스트와 관련 라이브러리 빌드를 수행한다.
- 검증된 항목마다 `fix:` 마이크로 커밋을 남긴다.
- 최종적으로 전체 솔루션 빌드·테스트와 변경 파일 줄 수를 확인한다.
- 실제 실행 결과와 제한사항은 `walkthrough.md`에 기록한다.
