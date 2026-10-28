# Modbus 보강 개발 결과

작성일: 2026-10-28. 승인 범위의 TCP Coil 기능, RTU Master, PLC 값 변환 구현 완료.

## 구현 내용

- TCP: FC01 읽기·FC15 쓰기와 메시지 생성기 추가. LSB 우선 Bit packing 및 수량/주소 범위 검증.
- 공통: Unit·Function·Protocol·Byte Count·쓰기 ACK 검증. 기존 읽기/쓰기 API에도 적용.
- 기존 IModbusMaster는 유지하고 IModbusCoilMaster를 추가해 구현자 호환성을 보존.
- PLC 값: Int32/UInt32/Single/Double/ASCII 문자열의 양방향 변환.
  BigEndian/ByteSwap/WordSwap/LittleEndian 네 가지 순서 지원.
- RTU: FC01/02/03/04/05/06/15/16, Serial 설정, DI 등록, CRC, 응답 전용 프레이밍.
- RTU 요청 전체 직렬화와 baud rate 기반 프레임 간 간격. 대기 중 취소/시간 초과와 송신 후 실패를 구분.
- 기존 TCP 명령/PDU 처리를 재사용하고 RTU 코덱이 MBAP 대신 Unit/CRC를 담당한다.
- 코어 엔진, 기존 양방향 RTU 코덱과 Serial 전송 계층은 수정하지 않음.

## 검증 결과

Release / .NET SDK 10.0.301, .NET 10, 현재 작업 트리에서 실행.

| 검증 | 결과 |
| --- | --- |
| Kable.Modbus.Tests | 102 통과, 실패/건너뜀 0 |
| Kable.Tests | 368 통과, 실패/건너뜀 0 |
| 신규 RTU 코덱 집중 테스트 (위 368개에 포함) | 13 통과 |
| 테스트 실행에 포함된 영향 프로젝트 Release 빌드 | 성공 |
| 신규/수정 Modbus 소스의 파일 크기 | 모두 300줄 미만 |
| 이번 변경의 staged diff 공백 검사 | 통과 |

총 470개 테스트가 통과했다. Modbus 테스트는 작업 전 37개에서 102개로 증가했고,
코어 테스트 프로젝트에 RTU 코덱 테스트 13개를 추가했다.
각 기능에서 신규 API 미구현으로 테스트가 실패하는 것을 먼저 확인한 뒤 구현했다.

재현 명령:

```powershell
dotnet test tests/Kable.Modbus.Tests/Kable.Modbus.Tests.csproj -c Release --nologo
dotnet test tests/Kable.Tests/Kable.Tests.csproj -c Release --nologo
```

검증한 장애: 잘못된 응답 헤더·길이·ACK·CRC, 정상 장비 예외 응답, 분할/연속/다중 세그먼트 입력,
송신 후 타임아웃/취소, 대기 중 타임아웃/취소, 물리 단절을 모사한 스트림 종료, Dispose 중 대기 요청 해제.
숫자 변환은 알려진 비트 패턴과 NaN 보존, 네 가지 순서, 음수/실수, 워밍업 후 할당 0을 검사했다.

## 제한

- 물리 COM/RS-485 장비는 검증하지 않았다. 드라이버·배선·방향 전환은 현장 확인이 필요하다.
- RTU 프레임 내부 t1.5 간격 감지, 어댑터 로컬 에코 제거, Slave, 브로드캐스트는 지원하지 않는다.
- 문자열은 ASCII만 지원한다. WordSwap은 값/필드에 포함된 모든 워드를 역순으로 바꾼다.
- 전체 통신은 무할당이 아니다. 숫자 변환 경로만 무할당 검증했다.
- Kable.Modbus는 현재 net10.0 대상이다. NuGet 게시와 전체 솔루션/타 OS 검증은 수행하지 않았다.
- 기존 미커밋 변경은 보존했다. 테스트 결과에는 현재 작업 트리에 있던 기존 변경도 반영되어 있다.

## 커밋

- a3093a1: TCP Coil 기능과 공통 응답 검증.
- e17af8c: PLC 값 변환과 테스트.
- RTU 구현 및 테스트, 사용 안내/결과 문서를 각각 후속 마이크로 커밋으로 기록.

사용 예제와 API 계약은 [Modbus 사용 안내](../../ko/MODBUS_USAGE.md)를 참조한다.
