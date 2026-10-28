# 설계 검토 6개 항목 개선

사용자 승인: 설계 검토 결과 전체 진행 요청 (2026-10-28).

## 범위와 순서

1. 정식 회귀 테스트를 먼저 추가: 정상 NO_FAULT 응답, 느린 스트림과 응답의 격리,
   잡음+완전한 Modbus 응답, 디코더 무진행, Abort 후 정리와 동시 Dispose,
   복구 작업 실패 중 MQTT/OPC UA 정리.
2. 디코더: 기존 bool 인터페이스 유지. 소비 진행이 있으면 재시도하고 성공 시 무진행은 거부.
   src/Kable/Engine/Decoding/에 계약 검증을 분리한다.
3. 분류/라우팅: 임의 ToString 부분 문자열 판별을 없애고 선택적 코덱 분류 계약 또는
   명시적 IsAlarmMessage를 사용한다. ASCII의 알려진 알람 prefix는 코덱에 둔다.
4. 스트림 전달: 응답 경로는 스트림 소비를 기다리지 않는다. Wait 모드에는 용량이 제한된 별도
   전달 대기열/워커를 두며 상한 초과는 명시적으로 세션을 종료한다. Drop 정책과 알람 저장 계약 유지.
5. 연결 수명: TCP/NamedPipe의 Abort·Dispose 분리, 완료 Task 공유, 모든 정리 단계 보장.
   Serial/TLS의 동일한 계약을 회귀 테스트로 확인한다.
6. MQTT/OPC UA: 복구 Task await가 실패해도 자원·채널·구독·identity를 반드시 정리하고 오류를 반환.
7. 세션 책임 분리: ResponseRouter(대기 요청), InboundDelivery(스트림 전달), AlarmSpoolWorker(저장)를
   실제 상태/행동을 소유하는 객체로 추출한다. 공개 KableSession은 수명/작업 조율 facade를 유지한다.
8. 전체 빌드/테스트, 샘플, .NET 8/10 패키지 소비 검증과 사용법/업그레이드 문서 갱신.

## 대상 파일

- src/Kable.Core/Codecs/IAlarmMessageClassifier.cs 및 IProtocolCodec.cs 문서.
- src/Kable/Codecs/AsciiLineCodec.cs.
- src/Kable/Engine/Decoding/, Messaging/, Streaming/, Alarms/의 내부 독립 클래스.
- KableSession의 생성/송수신/시작/종료 partial 파일 및 KableSessionOptions.
- src/Kable/Transports/TcpConnection.cs, NamedPipeConnection.cs.
- src/Kable.Mqtt/Subscriptions/, src/Kable.OpcUa/Subscriptions/.
- tests/Kable.Tests/Cases/Engine/Decoding/, Messaging/, Streaming/, Alarms/, Transports/.
- tests/Kable.Mqtt.Tests/Subscriptions/, tests/Kable.OpcUa.Tests/Subscriptions/.
- docs/ko/DEVELOPER_USAGE.md, ERROR_HANDLING.md, 릴리즈 노트 및 결과 문서.

모든 신규/수정 파일은 300줄 미만으로 나눈다. 테스트 폴더는 책임별로 대응시킨다.

## 보존할 계약

- IDeviceSession/IProtocolCodec 기존 구현자 호환성을 유지한다.
- FIFO 송신 후 타임아웃/취소는 연결 종료, CID는 해당 요청 실패로 유지한다.
- 응답을 알람으로 사용하지 않도록 명시적 분류 규칙을 먼저 적용한다.
- 알람 스풀의 Stored/InDoubt/Unprocessed 및 기존 종료 제한시간을 유지한다.
- 이번 범위의 수신 큐는 유한 상한을 갖는다. Wait backlog 상한 초과는 숨기지 않는다.
- 기존 소스는 승인 범위 안에서만 변경하고 검증된 기능 단위로 마이크로 커밋한다.
