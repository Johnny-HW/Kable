# 설계 보강 완료 및 검증

2026-10-28, Windows / .NET 8.0.27·10.0.9. 검증 대상 코드: `d7b90dc`.
사용자가 승인한 설계 검토 6개 항목 전체를 구현했다.

## 변경 결과

| 검토 항목 | 구현 및 회귀 검증 |
| --- | --- |
| 정상 응답의 알람 오분류 | 임의 ToString 부분 문자열 추측 제거. 선택적 IAlarmMessageClassifier와 명시적 옵션 사용. NO_FAULT/ALARM_CLOCK_READY/EMERGENCY_CONTACT 응답 및 명시적 알람 격리 검증 |
| 느린 스트림이 응답을 막음 | InboundDelivery가 별도 유한 전달 워커 소유. 소비자가 멈춰도 응답·알람 전달. Wait backlog 포화 시 명시적 종료, 3개 Drop 정책의 순서·유실 집계 검증 |
| 잡음 뒤 완전한 프레임 정체 | FrameDecoder가 false+소비 진행 시 즉시 재시도. 잡음과 Modbus 응답을 한 번에 전송, 분할 프레임, 성공 무진행 거부 검증 |
| Abort 후 TCP/파이프 정리 누락 | StreamConnectionLifetime이 Abort와 최종 정리 분리. 동시 Dispose 완료 작업 공유. 파이프라인·스트림·CTS 정리, 정리 실패 중 나머지 단계 수행, Abort/Dispose 동시 실행 검증 |
| 복구 작업 실패 중 MQTT/OPC UA 정리 누락 | 작업 실패를 수집한 뒤 SDK 자원·채널·구독·identity 정리. 실패 Task 주입 및 실제 로컬 OPC UA 세션 종료·자격 바이트 초기화 검증 |
| 세션 partial 간 책임 집중 | ResponseRouter가 FIFO/CID 예약·매칭·실패 전파, InboundDelivery가 전달·드롭, AlarmSpoolWorker가 큐·저장·집계를 소유. 단순 파일 이동이 아닌 상태 소유권 분리 |

알람 판별 델리게이트는 수신 메시지당 한 번만 실행한다. 생성 시 옵션을 복사한다.
기존 IDeviceSession/IProtocolCodec 구현자는 새 필수 멤버를 구현할 필요가 없다.
FIFO 송신 후 timeout/취소 시 종료, CID 개별 실패, 알람 스풀의
Stored/InDoubt/Unprocessed·종료 배출 계약은 유지했다.

## 최종 검증

`tools/release/VerifySolution.ps1`:

- 전체 Release 솔루션 빌드: 경고 0, 오류 0.
- 13개 테스트 프로젝트, 16개 런타임 실행: **815/815 통과**, 실패·건너뜀 0.
- 고유 테스트 689개 + Modbus/MQTT/OPC UA .NET 8 재실행 126회.
- Kable 코어: 399개. Modbus 110개 × 2, MQTT 4개 × 2, OPC UA 12개 × 2.
- 동시 Start 실패 공유 외부 진단 3회 통과, legacy generator package 진단 통과.
- QuickStart, Host IPC, ConfigStudio hidden-window, 공유 메모리 2회 UI 세션 재연결 smoke 통과.
- 기존 Serial·TLS/mTLS 및 알람 저장 실패/배출·부하 테스트 포함.

로그: `artifacts/design-hardening-solution-final.log`.
원본 TRX: `artifacts/release-readiness/solution-tests/`. 실행별 파일명·건수는 [검증 명세](verification.json)에 기록했다.

`tools/release/VerifyPackages.ps1`: **제품 패키지 14개 모두 통과**.
README·LICENSE·TFM·Analyzer 검사와 격리 캐시의 .NET 8/10 콘솔·WPF 소비자 실행을 확인했다.
WPF는 런타임별 컨트롤 12개를 로드했다. [패키지별 SHA-256 명세](packages.json).
최종 feed·소비자·캐시: `artifacts/release-readiness/742fc22fbacc40bf818f56e5eaad57fa/`.
로그: `artifacts/design-hardening-packages-final.log`.

## 업그레이드

[API 설정 예제](../../ko/DEVELOPER_USAGE.md#알람-분류와-느린-스트림-소비자-192),
[실패·종료 처리](../../ko/ERROR_HANDLING.md),
[배포·업그레이드 안내](../../ko/RELEASE_GUIDE.md),
[English migration](../../en/RELEASE_GUIDE.md#session-hardening-migration-192)를 갱신했다.

장비 고유 FAULT/EMERGENCY 프레임은 명시적인 분류기로 옮긴다.
Wait는 응답 경로를 무기한 멈추지 않으며, 별도 전달 대기열 포화 시
TelemetryBufferOverflowException으로 세션을 종료한다. 처리량에 맞는 큐 용량 또는 Drop 정책을 선택한다.
알람 저장 콜백은 취소 토큰을 준수해야 기존 저장/종료 제한시간이 작동한다.

실장비, 장기 운전 및 다른 OS의 검증은 이번 범위에 포함하지 않았다.
NuGet 게시·태그 생성·실제 배포는 수행하지 않았다.

## 기능 단위 커밋

- `9812d0c`: 명시적 알람 분류와 디코더 진행 계약.
- `19a3a9b`: 유한 텔레메트리 전달 격리.
- `49266f3`: Abort 이후 파이프라인 정리와 공유 Dispose.
- `9369e2e`: 복구 작업 실패 중 구독 자원 정리.
- `68b468f`: 응답 예약·알람 스풀 상태 소유권 분리.
- `d7b90dc`: Abort와 최종 폐기 동시 실행 직렬화.
