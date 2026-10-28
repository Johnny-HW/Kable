# Modbus 통신 보강 개발 범위

작성일: 2026-10-28. 사용자 승인: 보강 범위 설명 후 «개발범위를 문서화하고 개발 진행하세요» 지시.

## 목표와 제외 범위

- TCP: FC01 Coil 읽기, FC15 다중 Coil 쓰기 추가.
- RTU: Serial Master로 FC01/02/03/04/05/06/15/16 제공.
- PLC 값: Int32, UInt32, Single, Double, 문자열의 양방향 변환과 바이트/워드 순서 설정.
- 기존 IModbusMaster 구현자는 깨뜨리지 않는다. 새 IModbusCoilMaster 확장 계약을 사용한다.
- TLS, MQTT/OPC UA 구독, 일반 재연결 정책 변경, UI 변경, RTU Slave/브로드캐스트는 제외한다.

## 구현 순서와 대상 파일

모든 신규/수정 소스는 파일당 300줄 미만으로 유지한다.

1. TCP Coil 및 응답 검증
   - src/Kable.Modbus/IModbusCoilMaster.cs
   - src/Kable.Modbus/ModbusTcpMaster.cs 및 ModbusTcpMaster.Coils.cs
   - src/Kable.Modbus/Messages/ModbusTcpMessage.cs 및 ModbusTcpMessage.Coils.cs
   - src/Kable.Modbus/Protocol/ModbusValidation.cs
   - tests/Kable.Modbus.Tests/Messages/, Protocol/, Coils/ 대응 테스트
2. PLC 값 변환
   - src/Kable.Modbus/Data/PlcDataOrder.cs, PlcRegisterConverter.cs, PlcRegisterConverter.Strings.cs
   - tests/Kable.Modbus.Tests/Data/ 대응 테스트
3. RTU Master
   - src/Kable.Modbus/Rtu/ModbusRtuOptions.cs, ModbusRtuMaster.cs, ModbusRtuMaster.Operations.cs
   - src/Kable.Modbus/Codecs/ModbusRtuMasterCodec.cs
   - src/Kable.Modbus/ModbusServiceCollectionExtensions.cs
   - tests/Kable.Tests/Cases/Codecs/ModbusRtuMasterCodecTests.cs (프로젝트 참조 추가)
   - tests/Kable.Modbus.Tests/Rtu/ 인메모리 통합·장애 테스트
4. 사용 안내 및 검증 결과
   - docs/ko/MODBUS_USAGE.md
   - 이 폴더의 walkthrough.md

## 설계 계약

- Transports → Codecs → Engine 경계를 유지하며 기존 SerialPortConnectionFactory와 KableSession을 사용한다.
- RTU 전용 응답 코덱은 기존 CRC-16 엔진을 재사용한다. 기존 양방향 RTU 코덱은 변경하지 않는다.
- TCP와 RTU는 Modbus 명령 생성·결과 해석을 공유한다. RTU에서는 MBAP를 전송하지 않는다.
- RTU는 하나의 Master를 통한 요청을 직렬화하고 송수신 사이에 프레임 간 최소 대기를 적용한다.
- RTU Unit ID는 1..247; 브로드캐스트는 응답을 기다리는 API에서 거부한다.
- RTU 송신 후 타임아웃/취소는 기존 FIFO 세션을 종료한다. 자동 재전송하지 않는다.
- 응답의 Unit/Function/Protocol, byte count, 쓰기 ACK와 요청 범위를 검증한다.
- 수량 상한: bit read 2000, register read 125, multiple coils 1968, multiple registers 123.
- PLC 변환은 Span과 stackalloc을 사용한다. 문자열 반환 및 기존 메시지/비동기 API의 할당은 명시한다.
- 기존 미커밋 작업을 보존하고 이번 변경 파일만 선택적으로 커밋한다. 무관한 변경을 reset하지 않는다.

## 검증과 완료 기준

- 각 단계에서 먼저 실패하는 테스트를 확인하고 구현 후 통과시킨다.
- Coil bit packing(LSB 우선), 비정렬 수량, 주소 끝 경계와 잘못된 응답을 검사한다.
- PLC 변환은 독립된 알려진 비트 패턴, 네 가지 순서, 음수/NaN, 문자열 패딩과 용량을 검사한다.
- RTU는 알려진 CRC, 분할/연속 프레임, CRC 오류, 정상 예외 응답, 타임아웃, 취소, 단절, 직렬화를 검사한다.
- Modbus 전체 테스트와 Kable.Tests 회귀 테스트, 영향받는 프로젝트의 Release 빌드가 통과해야 한다.
- 물리 COM/RS-485 실장비 검증은 하드웨어가 없어 수행하지 않으며 제한으로 기록한다.
- 검증된 원자 단위마다 feat:/docs: 마이크로 커밋을 생성한다.
