# design_critique.md 기술 검토 결과

- 검토일: 2026-10-30
- 검토 대상: 같은 폴더의 `design_critique.md` 및 현재 구현
- 기준 커밋: `2f214c150cc652fe6a89b28f16f043a7488387ff`
- 방법: 소스 대조, net10.0 Release 검증 프로브, 관련 기존 테스트 실행
- 범위: 문서의 주요 결함·개선 권고 검토. 프로덕션 코드와 원문은 수정하지 않음

## 결론

원문에는 실제 수정할 결함 후보가 있다. 다만 재현된 동작, 정책 선택, 성능 추정,
런타임 동작에 대한 사실 오류가 섞여 있어 원문의 P0 분류와 전면 리팩토링 결론을 그대로 채택하면 안 된다.
`1.75 / 5.00` 점수 역시 통제된 비교 측정으로 산출된 제품 평가 점수로 사용할 수 없다.

우선순위는 헤더 검증, 단절 상태·진단 일관성, 정리 작업 예외 관찰, 하트비트 경계 조건,
할당·UI 성능 측정 순으로 잡는다. 공개 API 호환성과 메시지 소유권 계약을 유지한다.

## 1. 재현하거나 소스로 확인한 항목

| ID | 항목 | 확인 내용 | 권고 |
| --- | --- | --- | --- |
| V01 | Modbus TCP ProtocolId 검증 | ProtocolId=0x1234 프레임도 TryDecode가 true 반환. 실행 재현 | 표준 Modbus 모드의 헤더 거부·재동기화 테스트 우선 작성 |
| V02 | SLMP 서브헤더 검증 | 서브헤더=0xFFFF 프레임도 TryDecode가 true 반환. 실행 재현 | 지원하는 요청·응답 헤더만 수용하도록 검증 |
| V03 | 단절 상태·재시작 안내 | 단절 후 IsConnected=false, State=Running. StartAsync가 연결 없이 반환. 실행 재현 | 단일 수명 계약에 맞는 상태·재시작 결과·오류 안내 정리 |
| V04 | 정리 작업의 미관찰 예외 위험 | Task.Run 반환값 미보관, PerformCleanupAsync가 실패 Task를 await하는 경로 확인 | 실패 주입으로 미관찰 예외 재현·관찰 경로 확인 |
| V05 | 동기 연결·종료 | Dispose의 GetAwaiter().GetResult와 netstandard2.0 파이프 Connect 확인 | 호환성 유지하며 비동기 사용 경로와 동기 동작 개선 |
| V06 | 하트비트 갱신 기준 | 완성된 프레임을 디코딩한 뒤 수신 시각 갱신 | 바이트 활동·유효 프레임 생존 판정을 구분해 검증 |
| V07 | 할당·관찰자·UI 비용 | 페이로드 복사, 문자열 CID, Outbound 리스트, PCAP 락·복사, UI 선두 삽입 확인 | 각 경로를 따로 측정하고 병목부터 개선 |

V01·V02는 프레임 코덱의 수용 여부를 확인한 결과다. 상위 프로토콜 클라이언트가 해당 메시지를
최종 응답으로 사용하는지, 실제 장비 동작에 어떤 영향을 주는지는 별도 검증 대상이다.
V04·V06은 소스상 위험 경로 확인이며 프로세스 크래시나 하트비트 오탐을 실행 재현한 결과는 아니다.
V07 역시 할당·연산의 존재를 확인한 것으로 실제 처리량 저하 규모를 확정하지 않는다.

### 코드 근거

- V01: [ModbusTcpCodec](../../src/Kable.Modbus/Codecs/ModbusTcpCodec.cs) — ProtocolId를 읽고 메시지에 전달하지만 값 검증 없음.
- V02: [MelsecSlmpCodec](../../src/Kable.Melsec/Codecs/MelsecSlmpCodec.cs) — 요청 서브헤더 이외의 값을 응답 경로로 분기.
- V03: [Disconnect](../../src/Kable/Engine/Lifecycle/KableSession.Disconnect.cs), [Start](../../src/Kable/Engine/Lifecycle/KableSession.Start.cs), [Messaging](../../src/Kable/Engine/KableSession.Messaging.cs).
- V04: [세션 생성](../../src/Kable/Engine/KableSession.cs), [인바운드 실패 처리](../../src/Kable/Engine/Streaming/KableSession.InboundBuffer.cs), [Cleanup](../../src/Kable/Engine/Lifecycle/KableSession.Cleanup.cs).
- V05: [Cleanup](../../src/Kable/Engine/Lifecycle/KableSession.Cleanup.cs), [Named Pipe](../../src/Kable/Transports/NamedPipeConnection.cs), [IPC](../../src/Kable.Transport.Ipc/IpcNamedPipe.cs).
- V06: [ReadLoopAsync·HeartbeatLoopAsync](../../src/Kable/Engine/KableSession.Loops.cs).
- V07: [송신 배치](../../src/Kable/Engine/KableSession.Outbound.cs), [PCAP 관찰자](../../src/Kable/Observability/PcapStreamObserver.cs), [패킷 로그 UI](../../src/Kable.UI.Wpf/Controls/RawPacketLogStreamViewModel.cs).

## 2. 원문에서 정정할 주장

| 원문의 주장 | 검토 결과 | 적절한 표현 |
| --- | --- | --- |
| MemoryPool을 전혀 사용하지 않는다 | 기본 StreamPipeReaderOptions.Pool은 MemoryPool<byte>.Shared. 실행 확인 | 명시적 풀 주입은 없지만 BCL 기본 풀을 사용한다 |
| 무한 Delay가 영구 타이머 노드를 만든다 | .NET DelayPromise는 무한 제한시간에 타이머를 만들지 않음 | 취소 등록과 대기 Task의 유지·추가 할당 가능성을 조사한다 |
| TrimEnd가 항상 두 번째 문자열을 만든다 | 변경 없는 문자열은 같은 참조 반환. 실행 확인 | 디코딩 문자열과 추가 트리밍·params 배열 비용을 구분한다 |
| 할당 테스트를 의도적으로 조작했다 | 테스트 이름이 불완전 입력·상한 초과 경로임을 명시함. 의도 추정 근거 없음 | 정상 디코딩 성공 경로의 할당 측정이 부족하다 |
| 재시도 소진 후 중단은 결함이다 | MaxAttempts 이후 중단은 문서화된 정책 | 무한 복구가 필요한 사용 사례에는 추가 정책을 검토한다 |
| KeepAlive 미설정이면 반드시 2시간 뒤 감지한다 | 미활성 KeepAlive의 기본 시간만으로 감지 시점을 정할 수 없음 | 요청 제한시간·하트비트·OS 설정별 무응답 감지 시간을 측정한다 |
| 시리얼 분리 시 확정적으로 커널 데드락 | 장치·드라이버·런타임별 실행 증거 없음 | 물리 분리와 진행 중 I/O의 종료 경합을 실장비에서 재현한다 |
| 동기 Dispose가 반드시 UI 데드락 | 동기 대기는 확인되지만 컨텍스트 캡처·외부 구현 등에 따라 달라짐 | 호출 스레드 블로킹과 실제 데드락을 분리해 검증한다 |
| 임시 wpftmp 파일이 Git 형상 관리 중이다 | 검토 시점 git ls-files에 해당 파일 없음 | 디스크 잔여물과 추적 파일을 구분한다 |

### MemoryPool·Delay에 대한 런타임 근거

- [StreamPipeReaderOptions 구현](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/StreamPipeReaderOptions.cs): 명시하지 않은 Pool에 MemoryPool<byte>.Shared를 설정한다.
- [Task 구현](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs): DelayPromise는 무한 제한시간이면 타이머 생성을 생략한다.
- 무한 Delay에 취소 토큰을 넘기면 취소 등록은 생긴다. 장수명 토큰을 재사용하면 완료된 작업 이후에도
  등록이 유지될 수 있으므로 WaitAsync 또는 등록 해제 가능한 호환 헬퍼로 개선할 가치는 있다.
- RequestAsync는 연결된 CTS를 using으로 정리한다. 모든 요청이 영구적으로 누적된다는 단정도
  호출 경로별 토큰 소유권 검증 없이 할 수 없다.

## 3. 실제 현상과 설계 정책을 구분할 항목

### 단절 세션의 수명

V03의 상태·반환 동작은 재현했다. 하지만 단절된 객체를 재사용할 수 없다는 사실 자체는
[단일 세션 수명 계약](../ko/CONNECTION_LIFECYCLE.md)에 부합한다.
수정 목표는 동일 객체의 무조건 재연결이 아니라 종료된 세션의 일관된 상태 표현과 명확한 사용 안내다.
건강 상태와 세션 수명 상태의 역할도 구분해서 정의해야 한다. 이를 곧바로 P0 좀비 버그라고 단정하지 않는다.

### 텔레메트리 포화와 백프레셔

[InboundDelivery](../../src/Kable/Engine/Streaming/InboundDelivery.cs)는 Wait 모드에 유한 백로그를 두며,
포화 시 응답 라우팅을 막는 대신 실패시키는 정책이다. Drop 계열 모드도 제공한다.
두 단계 큐가 항상 존재하거나 모든 메시지가 반드시 두 번 컨텍스트 스위칭한다는 주장은 부정확하다.

원문의 권고처럼 소비자가 느릴 때 수신 루프 전체를 멈추면 같은 연결의 요청 응답과 알람까지
정체될 수 있다. 유실 허용 여부·응답 진행 보장·큐 한도를 함께 결정해야 한다.

### 하트비트 생존 판정

바이트가 계속 도착하지만 완성 프레임이 없는 경우와, 정상 프레임을 계속 수신하는 경우를 구분한다.
무조건 원시 바이트 도착만으로 생존 시각을 갱신하면 잘못된 데이터가 세션을 계속 유지할 수도 있다.
느린 대형 프레임·불완전 프레임·무효 데이터에 대한 제한시간과 생존 정책을 함께 검증한다.

### 메시지 소유권과 제로카피

[IProtocolCodec](../../src/Kable.Core/IProtocolCodec.cs)의 입력 버퍼 수명 이후 데이터 소유 계약은
사용자가 메시지를 안전하게 보관하도록 하기 위한 것이다. 복사를 제거해 PipeReader 버퍼를 그대로
반환하면 AdvanceTo 이후 재사용된 메모리를 읽을 위험이 있다.

IMemoryOwner 또는 별도 소유 메시지 모델은 검토할 수 있지만, 해제 주체·예외 경로·다중 전달과
호환성을 함께 설계해야 한다. 복사 존재만으로 인터페이스 전체를 결함으로 판정하지 않는다.
문자열·질의 경로의 실제 할당 범위는 [성능 기준](../ko/PERFORMANCE_BASELINE.md)에 이미 명시되어 있다.

### 스트림·재연결 API

- Stream의 채널 소비는 브로드캐스트가 아니다. 다중 소비자 계약을 문서화할 필요는 있지만,
  IAsyncEnumerable이라는 타입이나 Stream이라는 이름만으로 Pub/Sub 보장 위반이라고 확정할 수 없다.
- RetryDelay 기반 대기는 주기 검사 방식이다. CPU를 계속 점유하는 busy spin과 구분한다.
- 재연결 중 이전 클라이언트의 참조 획득과 폐기가 경합할 가능성은 별도 재현 테스트가 필요하다.
- MaxAttempts 소진 후 RecoveryTask가 완료되는 동작은 명세와 일치한다.
- IDisposable 제거, IProtocolCodec 서명 교체, Stream 의미 변경은 기존 사용자를 깨뜨릴 수 있다.
  [시스템 설계 기준](../SYSTEM_DESIGN.md)의 공개 인터페이스 호환성 원칙을 먼저 적용한다.

## 4. 실행 검증 기록

별도 검증 프로브는 artifacts/design-review-probes에 두었다. 제품 코드에는 포함하지 않았다.

| 입력·조건 | 실제 결과 |
| --- | --- |
| Modbus: `00 01 12 34 00 02 01 03` | ProtocolId=0x1234인 프레임을 TryDecode가 수용 |
| SLMP: `FF FF 00 FF FF 03 00 02 00 00 00` | 서브헤더=0xFFFF인 프레임을 TryDecode가 수용 |
| new StreamPipeReaderOptions().Pool 검사 | MemoryPool<byte>.Shared와 같음 |
| 문자열 OK에 TrimEnd 적용 | 원본과 같은 문자열 참조 |
| InMemory 연결 시작 후 Abort | IsConnected=false, State=Running |
| 위 단절 세션에 StartAsync 재호출 | 예외 없이 반환, 실제 연결은 복구되지 않음 |

헤더 프로브는 필드 수용 여부에 초점을 두며 완전한 기능 코드별 PDU 검증은 포함하지 않는다.
TCP Half-Open, 시리얼 물리 분리, UI 데드락, 미관찰 예외 및 대형 프레임 하트비트는
이번 실행으로 재현한 항목에 포함하지 않는다.

기존 테스트는 다음 필터로 11개 통과했다.

```powershell
dotnet test tests/Kable.Tests/Kable.Tests.csproj -c Release --filter "FullyQualifiedName~Heartbeat|FullyQualifiedName~SessionDisconnectResilience|FullyQualifiedName~ModbusRtuCodecAllocation" --verbosity quiet
```

기존 테스트 통과와 위 프로브의 문제 재현은 모순이 아니다. 서로 다른 조건과 검증 경로를 다룬다.
앞서 수행한 [오류 비교](../ko/benchmarks/FaultComparison.md)도 취소·FIN·늦은 응답 등
명시된 범위의 결과이며 모든 프레이밍·수명 경계를 보장하는 근거가 아니다.

## 5. 권고하는 처리 순서

| 순서 | 작업 | 완료 판단 |
| --- | --- | --- |
| 1 | V01·V02 헤더 검증 | 잘못된 헤더 거부와 분할·연속 프레임 처리 회귀 테스트 통과 |
| 2 | V03 상태·오류 안내 정리 | 단절 뒤 상태·StartAsync·요청 실패가 단일 수명 계약에 맞게 일치 |
| 3 | V04 정리 예외 관찰 | 실패 주입 후 모든 정리 작업의 결과가 관찰되고 대기자가 종료 |
| 4 | V05·V06 비동기 종료·하트비트 경계 | 대상 프레임워크와 지연·분할 수신 조건의 회귀 테스트 통과 |
| 5 | V07 성능 개선 | 동일 조건의 전후 할당량·처리량·p99와 자원 측정으로 효과 확인 |

이 순서는 검토 제안이며 구현 승인이나 완료를 뜻하지 않는다.
인터페이스 변경·소유권 변경·전송 정책 변경은 별도 계획과 승인 후 진행한다.

## 6. 원문 품질 보완 사항

- 감사 대상 커밋을 고정하고 현재 코드와 다른 인용·줄 번호를 갱신한다.
- 결함 ID와 본문 항목을 연결하고, 항목마다 재현 절차·기대 동작·실제 동작·영향 범위를 적는다.
- 정적 분석으로 확인한 코드와 실행 재현한 장애를 구분한다.
- 테스트 작성자의 의도나 조작을 근거 없이 추정한 표현을 제거한다.
- 확인하지 않은 프로세스 크래시·커널 데드락·8초 감지 보장 등의 단정을 수정한다.
- 종합 점수와 산업 표준 비교는 평가 조건·측정 근거가 확보되기 전 확정 판정으로 사용하지 않는다.
