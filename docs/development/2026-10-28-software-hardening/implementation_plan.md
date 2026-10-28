# 실장비 제외 통신 보강

사용자 승인: 실장비 외 검증 가능한 전체 범위 설명 후 «진행해줘» (2026-10-28).

## 단계와 대상 파일

각 소스 파일은 300줄 미만으로 나누고 테스트를 먼저 추가한다.

1. Serial 수명: src/Kable/Transports/SerialPortConnection.cs 및 SerialPortConnectionFactory.cs.
   tests/Kable.Tests/Cases/Transports/SerialLifecycleTests.cs에서 취소·Abort 후 정리·실패 정리 확인.
2. 이름별 Modbus 장비: src/Kable.Modbus/Devices/와 tests/Kable.Modbus.Tests/Devices/.
   팩토리 지연 생성, 이름 중복 거부, 장비별 수명과 등록 독립성.
3. PLC 태그: src/Kable.Modbus/Data/PlcTag*.cs와 tests/Kable.Modbus.Tests/Data/.
   주소·자료형·순서·배율·오프셋·단위 및 문자열 정의로 읽기/쓰기.
4. TLS/mTLS: src/Kable/Transports/Tls/와 tests/Kable.Tests/Cases/Transports/Tls/.
   SslStream 클라이언트/서버 어댑터와 Builder 확장, 기본 인증서 검증, 취소와 실패 시 정리.
   로컬 TCP·테스트 인증서로 정상 통신, 신뢰/이름 오류, 클라이언트 인증 검증.
5. MQTT 구독: src/Kable.Mqtt/Subscriptions/와 tests/Kable.Mqtt.Tests/Subscriptions/.
   유한 수신 큐, QoS, 토픽 필터, 재접속/재구독, 해제와 종료. 로컬 MQTTnet 브로커 검증.
6. OPC UA 구독·일괄 처리: src/Kable.OpcUa/Subscriptions/ 및 Batch/와 대응 테스트.
   기존 인터페이스 유지, 새 API로 monitored item 알림과 재연결 시 복원, 취소·해제.
   공식 SDK의 로컬 서버를 이용한 통합 검증.
7. 모의 부하/장애: tests/Kable.Modbus.Tests/Soak/와 기존 코어 soak.
   다중 장비 반복 요청, 단절/지연 주입, 취소 후 작업 종료와 제한된 큐 검증.

## 원칙

- 기존 작업 트리 변경은 보존하며 이번 파일만 커밋한다.
- Transport → Codec → Engine 경계를 유지하고 기존 공개 인터페이스를 깨뜨리지 않는다.
- 숫자 변환 외 전체 통신을 무할당으로 주장하지 않는다.
- SerialPort.Open은 OS 동기 API이므로 취소 가능한 비동기 경계와 취소 후 늦게 열린 자원 정리를 제공한다.
  네이티브 드라이버 호출을 강제로 중단한다고 보장하지 않는다.
- TLS는 기본 신뢰/호스트명 검증을 유지한다. 테스트는 연결 단위로만 신뢰 정책을 설정한다.
- 재연결은 연결과 구독을 복원하며 쓰기/명령을 재전송하지 않는다.
- 단계별 대상 테스트와 Release 빌드 통과 후 마이크로 커밋.
- 마지막에 영향 프로젝트 회귀 테스트, 코어 멀티 타깃 빌드, 사용법·결과 문서를 기록한다.

## 제외 및 한계

물리 COM/USB/RS-485, 배선/노이즈/방향 전환, 제조사 특성, 타 OS 드라이버 동작은 제외한다.
자동화된 수분 단위 soak는 24시간/수일 현장 운전 보증이 아니다. 실행 시간과 관측량을 결과에 명시한다.
