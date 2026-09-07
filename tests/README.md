# 01.Kable Test Suites (`tests/`)

`02.SoftwareLib/01.Kable/tests`는 고성능 통신 엔진의 파이프라인 무결성, 바이트 단편화(Fragmentation), 락프리 동시성 및 통신 단절 장애 복구(Fault Injection)를 검증하는 광범위한 테스트 스위트입니다.

---

## 🧪 테스트 프로젝트 구성

| 테스트 프로젝트 | 검증 대상 | 핵심 테스트 항목 |
| :--- | :--- | :--- |
| **`Kable.Tests`** | 전송/코덱/세션 통합 | • `ByteFragmentationTests`: TCP 패킷 쪼개짐/붙음 상황에서의 완벽한 버퍼 복원<br/>• `CodecAdvancedFramingTests`: 구분자 및 길이 기반 프레이밍<br/>• `WatchdogTimeoutAndDisconnectTests`: 케이블 탈락 시 Fail-Fast 안전 정지<br/>• `SessionInterleavingAndResilienceTests`: 동시 다발적 요청 간 배타 제어 및 파이프라이닝<br/>• `TelemetryRingBufferTests`: 초당 수만 건 인입 시 메모리 무결성 |
| **`Kable.Engine.Disruptor.Tests`** | 락프리 링버퍼 | • `SpscRingBufferTests`: 멀티스레드 대용량 스트레스 및 캐시 패딩 정합성 |
| **`Kable.Generators.Tests`** | Roslyn 소스 생성기 | • `SourceGeneratorExecutionTests`: 컴파일 타임 직렬화 코드 자동 생성 검증 |
| **`Kable.Transport.Ipc.Tests`** | 프로세스 간 IPC | • `IpcTransportIntegrationTests`: 로컬 NamedPipe 양방향 고속 통신 |
| **`Kable.Observability.OpenTelemetry.Tests`** | 분산 추적/메트릭 | • `KableMetricsCollectorTests`: Activity Span 및 지연 시간 메트릭 측정 |

---

## 🚀 테스트 실행 방법

```powershell
dotnet test D:/Johnny/00.New/02.SoftwareLib/01.Kable/Kable.slnx --logger "console;verbosity=detailed"
```
