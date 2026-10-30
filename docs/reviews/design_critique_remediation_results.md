# 종합 검토 수정 결과

2026-10-30 · 기준 커밋 `17e7d22` 이후 수정

**잘못된 응답의 성공 처리, 수신 진행 중단, 종료 정리 누락을 수정했다.**
공개 인터페이스와 단일 세션 수명 계약은 유지했다.
원본 감사·재감사 문서는 수정 당시의 증거로 보존한다.

## 1. 무엇이 달라졌나

| 검토 항목 | 수정 후 동작 | 주요 검증 |
|---|---|---|
| V02 MELSEC 검증 | 알려진 요청·응답 서브헤더만 디코딩. 성공은 응답이면서 EndCode=0인 경우만 인정. 읽기는 요청 워드 수와 정확히 일치해야 하고 쓰기 성공 응답은 빈 데이터여야 함 | 잘못된 헤더의 성공 판정 거부, 빈·홀수·부족·초과 읽기 응답 거부, 기존 정상 읽기·쓰기 |
| V01 Modbus TCP ProtocolId | 완성된 유효 길이 프레임의 ProtocolId가 0이 아니면 프로토콜 오류. CID 라우터에 유효 응답으로 전달하지 않음 | 잘못된 ProtocolId 차단, 기존 MBAP 노이즈 건너뛰기와 정상 응답 |
| NEW-02 RTU 채널 | 디코더가 바이트를 소비했다면 같은 읽기 버퍼를 다시 검사. 성공 후 남은 바이트를 전부 examined로 표시하지 않음 | 노이즈 1바이트와 정상 응답을 한 번에 수신해도 성공 |
| NEW-03 RTU 분할 수신 | FC08은 고정 길이로 처리. 길이 규칙이 없는 함수는 수신된 버퍼 길이를 프레임 길이로 단정하지 않고 CRC 후보와 최대 크기로 제한 | FC08의 모든 분할 위치에서 입력 보존 후 전체 프레임 성공 |
| NEW-01 IPC·gRPC 종료 | Abort와 Dispose의 상태를 분리. Abort 후에도 파이프·CTS 정리와 gRPC 펌프 종료 대기 수행. 동시 Dispose는 같은 작업을 기다림 | Abort→Dispose, 반복 종료, 완료를 지연시킨 동시 종료, 기존 통합 왕복 통신 |
| NEW-07 IPC 리스너 | 대기 중인 accept만 리스너가 소유. 수락 완료한 스트림은 연결 객체로 소유권 이전. 취소·실패 경로에서 스트림 회수 | Stop 후 활성 연결 통신, 취소 후 재accept |
| NEW-09 시뮬레이터 | UseSimulator는 팩토리만 설정. 실제 연결 시마다 새 시뮬레이터를 생성하고 세션이 종료를 소유 | Build 전 미생성, 정상 왕복, 세션 종료 후 루프 완료, 같은 빌더의 독립 세션 |
| 추가 인메모리 수명 | Abort 후 Dispose에서도 자신이 소유한 reader/writer를 완료하고 CTS 해제 | 반대편 Input에 완료 전달 |
| NEW-04 RTT 지표 | 기록·초기화·스냅샷 복사를 같은 임계 구역으로 보호. 정렬은 밖에서 수행 | 동시 기록 50,000개 보존, 기록·조회·초기화 경쟁 |
| NEW-05 파형 공유 메모리 | 포인터 접근과 해제를 같은 수명 잠금으로 보호. 양수 용량·마스크·매핑 크기 검증. Magic은 마지막에 게시. CreateNew로 중복 초기화 차단 | 해제 후 접근 거부, 종료·접근 경쟁, 대기 깨우기, 중복 생성 시 기존 데이터 보존 |
| NEW-06 프레이밍 | 시작 마커 뒤에서 종료 구분자 탐색. 버퍼를 버리기 전 실제 길이 보존 | 동일 시작·종료 마커의 연속 프레임, Delimited/LengthField의 초과 길이 메시지 |
| V03 세션 상태 | 물리 단절 후 Stopping→Stopped. 종료된 세션의 StartAsync 거부. 오류 안내는 새 세션 생성으로 통일 | 단절 상태와 재시작 거부, 기존 수명·동시 시작 테스트 |
| V04 백그라운드 정리 | 정리 작업의 실패를 관찰하고 LastCleanupError에 보존. 제한시간 이후 완료되는 송수신 루프의 실패도 관찰 | 기존 알람 포화·정리 실패·수명 테스트. Stop/Dispose의 공유 실패 전파 유지 |
| NEW-08 로그 식별 | 세션 관찰자 어댑터가 모든 전달 기록에 DeviceId를 적용. 기존 SequenceNo와 ConnectionId 등 메타데이터 보존 | PLC-A 세션의 SEND 기록 식별 |
| NEW-10 CID 진단 | 관찰자 예외가 타임아웃을 대체하지 않음. 원인은 예외 Data["TraceException"]에 보존 | 관찰자가 실패해도 DeviceTimeoutException, CID 세션 유지 |
| NEW-10 상태 감시 | 지터 경고 검사에서 매 샘플 스냅샷 배열 생성 제거. 명시적 GetSnapshot은 기존 분석 계약 유지 | 알람을 발생시키지 않는 1,000회 기록에서 할당 0 B |
| CRLF 처리 | ASCII·UTF-8·Latin1은 문자열 변환 전에 줄 끝 제거. 다른 인코딩은 기존 문자 처리 유지 | LF와 CRLF 디코딩 할당량 동일, UTF-8 한글 및 빈 줄 |
| 취소 대기 | 최신 .NET은 Task.WaitAsync. netstandard는 수명이 끝나면 해제되는 취소 등록 사용 | 기존 취소·타임아웃·동시 시작 테스트 |
| 송신 배치 | 펌프마다 배치 목록을 재사용하고 송신 완료 처리 후 비움 | 기존 송신·배치·IPC·gRPC 통합 테스트 |
| 재연결 중 명령 | 캡처한 내부 클라이언트가 해제되면 명령은 DeviceDisconnectedException으로 실패. 재전송하지 않음 | 진행 중 해제 경합, 래퍼를 이미 해제한 후 호출은 기존 ObjectDisposedException 유지 |

## 2. 검증 결과

| 검증 | 결과 |
|---|---|
| 전체 솔루션 Release 빌드 | 경고 0, 오류 0. 설정된 netstandard2.0·net8.0·net10.0 등 대상 빌드 |
| 전체 솔루션 Release 직렬 테스트 | **941 통과, 0 실패, 0 건너뜀**. 대상 프레임워크별 실행을 합산한 수치 |
| 마지막 정리 루프 예외 관찰 보완 후 관련 테스트 | 수명·알람 스풀·하트비트 등 75 통과 |
| .NET 8 Modbus 전체 추가 확인 | 111 통과 |
| 변경 파일 검사 | git diff --check 통과. 이번에 추가한 C# 파일은 모두 300줄 미만 |

전체 실행 내역:

| 프로젝트 | 통과 수 |
|---|---:|
| Kable.Tests | 507 |
| Generators | 21 |
| Disruptor | 3 |
| IPC | 7 |
| Host | 5 |
| OpenTelemetry | 1 |
| gRPC | 6 |
| SharedMemory | 107 |
| Modbus | 111 × 2 대상 |
| MELSEC | 16 |
| MQTT | 4 × 2 대상 |
| OPC UA | 12 × 2 대상 |
| ProtocolPack | 14 |

최초 병렬 전수 실행에서는 .NET 8의 100ms RTU 타임아웃 테스트가 1회 실패했다.
그 테스트 단독 실행, .NET 8 Modbus 전체 실행, 직렬 전수 실행에서는 통과했다.
부하와 타이밍 민감성이 의심되지만 실패 원인을 확정하지 않았으며, 테스트 제한시간은 변경하지 않았다.

```powershell
dotnet build Kable.sln -c Release --verbosity quiet
dotnet test Kable.sln -c Release --no-build -m:1 --verbosity quiet --blame-hang-timeout 60s
dotnet test tests/Kable.Tests --filter 'FullyQualifiedName~Lifecycle|FullyQualifiedName~Spool|FullyQualifiedName~AuditSessionRegressionTests|FullyQualifiedName~Heartbeat'
```

원시 로그: [직렬 전수 실행](../../artifacts/re-audit-probes/solution-tests-serial.log),
[Release 빌드](../../artifacts/re-audit-probes/solution-build.log).
artifacts는 로컬 검증 자료이며 Git 추적 대상은 아니다.

## 3. 성능 비용

동기화에는 비용이 있다. 수정 전 `17e7d22`의 구현을 별도 이름공간으로 가져와
수정 후 구현과 같은 프로세스에서 교대 실행했다. Windows, .NET 10.0.9, Release,
워밍업 후 7회 중앙값이며 전수 테스트와 동시에 실행한 측정은 최종 수치에서 제외했다.

| 작업 | 수정 전 | 수정 후 | 반복 구간 힙 할당 |
|---|---:|---:|---:|
| RTT 샘플 기록, 회당 | 3.95 ns | 16.74 ns | 양쪽 0 B |
| 파형 1개 쓰기+읽기, 회당 | 156.91 ns | 169.97 ns | 양쪽 0 B |

RTT는 100만 회, 파형은 10만 회씩 실행했다. 파형 측정에는 이벤트 신호 비용도 포함된다.
이 수치는 같은 머신의 순차 마이크로벤치마크이며, 다중 프로세스 처리량이나 실제 장비 지연을 보장하지 않는다.
안전성 보완 비용을 포함한 결과이며 기존 경쟁 라이브러리 성능 순위를 다시 측정한 것은 아니다.
원시 자료: [측정 결과](../../artifacts/remediation-performance/results.json).

## 4. 사용 시 달라지는 점과 남은 범위

- UseSimulator의 설정 콜백은 UseSimulator 호출 때가 아니라 연결 시 실행된다. 실패도 StartAsync에서 받는다.
- SharedMemoryWaveformBuffer.Create는 기존 이름이 있으면 실패한다. 기존 버퍼에 연결하려면 Open을 사용한다.
  아직 초기화가 끝나지 않은 버퍼를 Open하면 유효한 버퍼로 수용하지 않는다.
  중복 생성 오류에는 버퍼 이름과 `DUPLICATE_WAVEFORM_BUFFER` 식별값을 제공한다.
  앱은 이 오류를 사용자에게 표시하고 해당 초기화를 중단한다. 중복 실행·이름 충돌을 먼저 확인하고,
  의도적으로 기존 버퍼에 참여하는 경우에만 Open을 선택한다. 라이브러리가 자동 연결하거나 프로세스를 강제 종료하지 않는다.
- 파형 버퍼는 계속 SPSC 계약이다. 수명 잠금이 여러 생산자·소비자 지원을 의미하지 않는다.
- Modbus TCP의 잘못된 ProtocolId는 유효 응답으로 무시하거나 임의 재동기화하지 않고 프로토콜 오류로 처리한다.
- RTU의 길이 규칙이 없는 함수는 CRC 후보 탐색을 사용한다. 임의 함수의 경계를 완벽히 추론한다는 보장은 없으며,
  추가 함수의 정식 지원에는 해당 함수 길이 규칙과 테스트를 추가해야 한다.
- 직렬 포트 드라이버의 실제 장애, .NET Framework에서의 실행, 장시간 현장 부하·전원 차단은 이번 검증에 포함하지 않았다.
- FIFO 타임아웃 종료, 유한 재연결 시도, 동기 Dispose 호환 API는 유지했다.
- 메모리 소유권·상관 키 타입을 바꾸는 공개 API 개편, 패키지 분할, 강제 TCP KeepAlive 기본값 변경은 적용하지 않았다.
- 성공 디코딩의 반환 배열·문자열 할당은 기존 소유권 계약을 유지한다. 이번 변경이 통신 전체 경로의
  0-GC를 달성했다는 의미는 아니며, 코덱의 XML 설명도 이 구분에 맞췄다.

## 5. 변경 기록

- `74aabc4` PLC 검증·RTU 진행·프레이밍 경계
- `58228f3` 전송 종료와 시뮬레이터 소유권
- `4205da1` 지표 동시성·파형 메모리 수명
- `e7743c0` 세션 상태·관찰자 식별·취소 및 종료 보완
- `d596676` CRLF·지터 검사 할당 최적화
- `3cf5633` 종료 대기 이후 루프 실패 관찰

이번 수정은 로컬 커밋까지 수행했다. 원격 푸시·패키지 배포·GitHub Pages 게시는 수행하지 않았다.
