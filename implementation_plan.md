# 테스트 실행 종료 지연 및 병렬 충돌 수정 계획

## 목적과 배경

`dotnet test Kable.sln` 실행에서 개별 테스트 결과는 통과로 출력되지만
`Kable.Tests`의 testhost가 종료되지 않아 전체 실행이 완료되지 않는다. 또한
TCP/Named Pipe 테스트에 부여된 `HardwareTransportTests` 컬렉션은 정의가 없어
직렬 실행이 보장되지 않으며, gRPC와 MQTT 테스트의 포트 선점 방식에는 포트
반납과 서버 바인딩 사이의 경쟁 조건이 있다.

목표는 전체 솔루션 테스트가 정상 종료하고, 동일 테스트를 반복 및 병렬 실행해도
포트/파이프 자원 경쟁으로 인해 간헐 실패하지 않도록 만드는 것이다.

## 범위

### 포함

- `Kable.Tests`가 종료하지 않는 정확한 원인(미완료 서버 작업 또는 미해제
  전송 자원)을 재현하고 제거한다.
- 하드웨어성 전송 테스트가 같은 xUnit 컬렉션에서 직렬 실행되도록 실제
  컬렉션 정의를 추가한다.
- gRPC/MQTT 통합 테스트가 안전하게 동적 포트를 사용하도록 개선한다.
- 정상 종료와 반복 실행 안정성을 자동 검증한다.

### 제외

- 제품 코드의 통신 프로토콜/공개 API 변경
- 테스트와 무관한 리팩터링
- 고정 포트가 필요한 외부 통합 환경의 구성 변경

## 변경 대상

- `[NEW] tests/Kable.Tests/Fixtures/HardwareTransportTestCollection.cs`
  - `[CollectionDefinition("HardwareTransportTests", DisableParallelization = true)]`
    를 선언한다. 필요한 경우 비어 있는 marker 클래스만 사용하며 fixture를
    추가하지 않는다.

- `[MODIFY] tests/Kable.Tests/Cases/Transports/NamedPipeTransportTests.cs`
  - 서버 task와 세션의 종료 순서를 확인한다.
  - 실패/취소 경로에서도 서버 파이프와 task가 남지 않도록 `try/finally` 및
    취소 토큰을 적용한다.

- `[MODIFY] tests/Kable.Tests/Cases/Transports/TcpListenerAndDiTests.cs`
  - listener, 서버 context, 서버 task를 항상 종료/await하도록 정리한다.
  - 테스트 본문의 모든 비동기 작업이 테스트 반환 전에 완료되는지 보장한다.

- `[MODIFY] tests/Kable.Tests/Cases/Transports/TransportAndBuilderTests.cs`
  - TCP listener/socket/server task의 정상·실패 경로를 `try/finally`로 정리하고,
    동기 `Receive`/`Send`는 비동기 I/O로 교체한다.

- `[MODIFY] tests/Kable.Tests/Cases/Transports/TransportFaultInjectionTests.cs`
  - 각 fault-injection 테스트에서 listener, socket, pipe, accept task를 확실히
    해제한다.
  - 단순 지연(`Task.Delay`)에 의존한 연결 단절 검출은 가능한 경우 명시적인
    신호/시간 제한으로 바꿔 플래키성을 줄인다.

- `[MODIFY] tests/Kable.Grpc.Tests/GrpcTransportIntegrationTests.cs`
  - 사전 포트 탐색 후 닫는 방식 대신 서버가 실제로 바인딩한 endpoint를
    읽거나, 테스트 수명 동안 포트 예약을 유지하는 방식으로 경쟁 조건을 제거한다.
  - host 종료를 `finally`에서 보장한다.

- `[MODIFY] tests/Kable.Mqtt.Tests/MqttTelemetryTests.cs`
  - MQTT 서버의 동적 포트 바인딩을 서버 옵션/실제 endpoint 기준으로 구성하고,
    서버 중지를 `finally`에서 보장한다.

## 실행 순서

1. 현재 상태에서 `Kable.Tests`만 단독 실행하고, 종료 시점에 남는 testhost 및
   네트워크/파이프 핸들을 확인한다. 필요하면 실패 테스트를 하나씩 필터링해
   종료를 막는 최소 케이스를 찾는다.
2. `HardwareTransportTests` 컬렉션 정의를 추가하고, 기존 네 개 전송 테스트
   클래스가 해당 컬렉션에 정확히 속하는지 확인한다.
3. 최소 재현 케이스부터 서버 task와 전송 자원 해제를 `try/finally`와 취소
   경로로 보강한다. 각 단위가 녹색이 된 뒤 다음 테스트로 이동한다.
4. gRPC/MQTT의 포트 할당을 경쟁 조건 없는 방식으로 변경하고, host/broker
   종료 경로를 검증한다.
5. 반복 실행 및 솔루션 전체 실행으로 종료/격리 문제를 확인한다.

## 검증 계획

1. 빌드: `dotnet build Kable.sln --no-restore`
2. 영향 테스트:
   - `dotnet test tests/Kable.Tests/Kable.Tests.csproj --no-build`
   - `dotnet test tests/Kable.Grpc.Tests/Kable.Grpc.Tests.csproj --no-build`
   - `dotnet test tests/Kable.Mqtt.Tests/Kable.Mqtt.Tests.csproj --no-build`
3. 안정성: 위 세 프로젝트와 `Kable.Tests`를 각각 최소 5회 반복 실행한다.
   각 실행은 실패 0, testhost 잔존 0, 명령 정상 종료(exit code 0)여야 한다.
4. 회귀: `dotnet test Kable.sln --no-build --verbosity minimal`이 정상 종료하며
   실패 0이어야 한다.
5. 완료 후 `Get-Process testhost,dotnet`으로 이번 실행에서 생성된 testhost가
   남아 있지 않은지 확인한다.

## 위험과 롤백

- 포트 예약 구현은 테스트 프레임워크나 호스트 API 제약에 따라 선택지가 달라질 수
  있다. 실제 바인딩 endpoint를 얻는 방식을 우선한다.
- 직렬화는 실행 시간을 다소 늘릴 수 있지만 전송 자원 충돌 방지가 우선이다.
- 각 변경 단위는 독립적으로 테스트 후 `test:` 또는 `fix:` 마이크로 커밋으로
  남긴다. 문제가 생기면 해당 마지막 검증 커밋으로만 되돌린다.

## 승인 요청

이 계획은 테스트 코드와 테스트 지원 파일만 변경한다. 승인 후 1단계의 재현부터
순차적으로 실행한다.
