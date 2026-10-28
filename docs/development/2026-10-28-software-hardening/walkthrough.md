# 실장비 제외 통신 보강 결과

2026-10-28. 사용자가 승인한 소프트웨어 보강 범위를 구현하고 자동 검증했다.

## 완료 범위

1. Serial: 연결 전/중 취소, 늦게 열린 자원 정리, Abort 후 Dispose, 동시 Dispose 완료 공유.
   기존 동기 SerialPort.Open을 ThreadPool에 격리하고 호출자 취소를 비동기로 처리한다.
2. 다중 장비: 이름별 독립 등록과 지연 생성, TCP/RTU 혼합 DI, 중복 이름·인스턴스 거부,
   정리 오류가 다른 장비 정리를 막지 않도록 집계하며 종료 중 재진입을 거부한다.
3. PLC 태그: 주소·자료형·순서·배율·오프셋·단위·입력/홀딩 영역 정의. 실제 값 읽기와 역변환 쓰기.
4. TLS/mTLS: 클라이언트/서버 스트림 어댑터, Builder 확장, 기본 인증서 검증, 연결/핸드셰이크 취소와 제한시간.
5. MQTT: 필터·QoS 구독, 소유 메시지와 유한 수신 큐, 드롭 수 집계, 재접속·재구독, 구독 해제·종료.
6. OPC UA: monitored item 알림, 유한 구독 큐·드롭 수, 일괄 읽기·쓰기와 개별 결과,
   KeepAlive 기반 새 세션/구독 복원, 취소 및 구독·Bridge 종료.
7. 모의 장애/부하: TCP 2대 + RTU 2대 동시 요청과 지연 주입, 장비 단절 격리,
   브로커/OPC 서버 재시작, 잘못된 인증서·응답·노드, 큐 포화, 수명 정리 검증.

새 공개 기능은 기존 인터페이스 변경 없이 추가했다. 원래 작업 트리의 미커밋 변경은 보존했다.

## 검증 결과

환경: Windows / .NET SDK 10.0.301, .NET 10.0.9, Release.

| 프로젝트/검증 | 결과 |
| --- | --- |
| Kable.Tests 전체 | 379 통과 |
| Kable.Modbus.Tests 전체 | 110 통과 |
| Kable.Mqtt.Tests 전체 | 3 통과 |
| Kable.OpcUa.Tests 전체 | 4 통과 |
| 합계 | 496 통과, 실패/건너뜀 0 |
| Kable 멀티 타깃 빌드 | netstandard2.0 / net8.0 / net10.0 성공, 경고·오류 0 |
| 영향 확장 프로젝트 | 각 테스트의 Release 빌드 성공 |
| 신규/수정 소스 파일 | 파일당 300줄 미만으로 분리 |

TLS 테스트는 실제 로컬 TCP와 자체 서명 테스트 인증서를 사용했다.
신뢰하지 않는 서버, 잘못된 호스트명, 클라이언트 인증서 누락을 거부하고 핸드셰이크 취소도 확인했다.
테스트 인증서를 OS의 전역 신뢰 저장소에 설치하지 않았다.

MQTT는 MQTTnet 로컬 브로커를 중지/재시작해 재구독 후 새 메시지를 확인했다.
수신 큐 2개에 20개를 전달한 테스트에서 18개 드롭을 확인했다.
OPC UA는 공식 SDK 로컬 서버에서 값 쓰기→변경 알림, 서버 재시작→구독 복원,
잘못된 노드의 부분 실패, 취소, 큐 포화와 종료를 확인했다.

## 지속 부하 관측

- `KABLE_SOAK_SECONDS=120`으로 TCP 2대 + RTU 2대 테스트를 별도 실행해 2분 동안 통과.
- 각 장비는 독립 기대값을 확인하고 31회마다 지연을 주입했다.
- 완료 후 모든 부하 작업을 await하고, 연결 정리 및 관리 힙 잔류 증가 16MiB 미만 기준을 통과했다.
- 최종 3초 회귀 실행의 TRX 관측: 3.02초, 장비별 5983/5983/304/303회 요청 완료,
  관리 힙 차이 -3,396,280바이트. 이는 해당 짧은 실행의 관측치이며 2분 실행의 수치가 아니다.
- 기존 코어 FIFO/CID soak 테스트도 전체 회귀 테스트에 포함되어 통과했다.

2분/3초 테스트가 24시간 이상 운전의 메모리 누수 부재나 현장 성능을 보장하지는 않는다.

## 재현 명령

```powershell
dotnet build src/Kable/Kable.csproj -c Release
dotnet test tests/Kable.Tests/Kable.Tests.csproj -c Release
dotnet test tests/Kable.Modbus.Tests/Kable.Modbus.Tests.csproj -c Release
dotnet test tests/Kable.Mqtt.Tests/Kable.Mqtt.Tests.csproj -c Release
dotnet test tests/Kable.OpcUa.Tests/Kable.OpcUa.Tests.csproj -c Release
$env:KABLE_SOAK_SECONDS='120'
dotnet test tests/Kable.Modbus.Tests/Kable.Modbus.Tests.csproj -c Release --filter FullyQualifiedName~MultiDeviceSoakTests
Remove-Item Env:KABLE_SOAK_SECONDS
```

전체 회귀의 원본 TRX는 각 테스트 프로젝트의 무시된 TestResults/에 software-hardening-*.trx로 보존했다.
더 긴 부하는 환경 변수로 시간을 늘려 실행할 수 있다.

## 남는 제한

- 물리 COM/USB/RS-485 드라이버·방향 전환·배선·노이즈·실제 탈착과 제조사 특성은 제외했다.
- Serial 네이티브 Open을 강제 취소할 수 없다. 호출자는 취소되며 드라이버 반환 후 자원을 정리한다.
- MQTT/OPC UA의 유한 큐 포화 시 새 데이터를 버린다. 영속 저장/무손실 업무 처리가 필요하면 별도 계층이 필요하다.
- 재연결은 연결·구독을 복원한다. 끊긴 동안의 누락 값을 재생하거나 실패한 쓰기를 재전송하지 않는다.
- OPC UA의 기존 익명·비보안 엔드포인트 선택 정책은 변경하지 않았다. 이번 TLS는 별도 TCP 전송 기능이다.
- 전체 솔루션, 다른 OS, NuGet 게시와 실시간 정밀도는 검증 범위에 포함하지 않았다.

사용법: [소프트웨어 보강 안내](../../ko/SOFTWARE_HARDENING_USAGE.md).
