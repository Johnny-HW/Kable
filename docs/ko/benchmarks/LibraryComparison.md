# TCP 라이브러리 성능 비교

## 목적과 범위

Kable, TouchSocket, SuperSocket, DotNetty와 .NET 기본 API로 동일한 ASCII 요청–응답 작업을 실행한다.
이는 실제 라이브러리를 사용하는 애플리케이션 수준 비교이며, 엔진 내부 처리나 물리 장비 지연을 분리한 측정이 아니다.
프로덕션 패키지에는 비교 대상의 의존성을 추가하지 않는다.

| 대상 | 고정 패키지 버전 | 사용 경로 |
| --- | --- | --- |
| Kable | 현재 저장소 소스 | KableSimple / QueryAsync |
| .NET 기본 API | net10.0 런타임 | TcpClient / NetworkStream / StreamReader |
| TouchSocket | 4.3.11 | TcpClient / Received + 벤치마크의 LF 파서·응답 큐 |
| SuperSocket.Client | 2.1.0 | EasyClient / LF 필터 / 지속 수신 콜백 + 응답 큐 |
| DotNetty.Transport·Codecs | 0.7.6 | Bootstrap / LineBasedFrameDecoder / StringDecoder·Encoder + 응답 큐 |

TouchSocket의 WaitingClient를 비교하는 테스트는 아니다. TouchSocket·DotNetty의 응답 큐와
SuperSocket 필터·응답 큐 코드는 `tests/Kable.Comparison.Benchmarks/Adapters/`에 공개한다.
기본 API는 읽기 쉬운 StreamReader 구현이며, 최적화된 Socket/Pipelines 구현의 성능 상한을 뜻하지 않는다.
Kable의 응답 라우팅·타임아웃 계약과 다른 구현의 기능은 완전히 같지 않다.

## 공통 조건

- 로컬 IPv4 TCP 에코 서버, ASCII, LF 한 바이트 종결자, 양쪽 NoDelay=true.
  SuperSocket은 SocketConnector의 기본 NoDelay=true를 사용한다.
- 페이로드 32/256/1024바이트. 바이트 수는 LF를 제외한다. 요청과 응답은 같은 내용이다.
- 연결 1/16/64개. 연결당 미완료 요청은 정확히 한 개이며, 연결끼리는 동시에 동작한다.
- 연결 유지·요청마다 즉시 송신. TLS, 로깅, 재연결, 디스크·UI 처리는 제외한다.
- DotNetty 연결들은 최대 4개의 이벤트 루프 스레드를 공유한다.
- 연결은 순차 생성한다. 원시 결과의 ConnectBatchMs는 전체 연결 준비 시간으로,
  라이브러리 초기화·이벤트 루프 준비를 포함하며 순수 네트워크 연결 지연은 아니다.
- 연결 준비와 워밍업은 본 측정에서 제외한다. 모든 실제 응답의 내용을 검증한다.
- 기본 시나리오당 총 요청 목표는 10,000개다. 연결별 동일 작업 수를 위해 올림하므로
  16/64개 연결에서는 실제 작업 수가 목표보다 커질 수 있다. Operations에 실제 개수를 기록한다.
- 연결별 워밍업 200회. 라이브러리별 별도 프로세스를 3회 실행하며 순서를 고정 시드로 섞는다.
  각 프로세스는 9개 시나리오를 순서대로 실행하므로 시나리오마다 새 프로세스는 아니다.

## 측정값과 해석

| 측정값 | 의미 |
| --- | --- |
| MeanUs, P50Us, P95Us, P99Us | 요청 API 진입부터 응답 반환까지 관측한 개별 요청 시간 |
| RequestsPerSecond | 전체 완료 요청 수 / 동시 요청 구간의 벽시계 시간 |
| AllocatedBytesPerOperation | 프로세스 전체 관리 힙 할당 증가량 / 전체 완료 요청 수 |
| CpuMs | 측정 구간의 프로세스 CPU 시간 합계; 다중 코어에서 벽시계보다 클 수 있음 |
| Gen0/1/2 | 측정 구간의 세대별 GC 횟수 증가량 |

할당량은 `GC.GetTotalAllocatedBytes(true)`를 사용하여 비동기 작업의 다른 스레드를 포함한다.
할당·CPU·GC에는 같은 프로세스에 있는 모의 서버와 측정 작업 생성 비용도 포함된다.
샘플 배열은 미리 생성한다. 문자열 변환과 요청별 송신 버퍼 생성은 각 구현의 측정 비용에 포함한다.
네이티브 메모리·상주 메모리·엔진 단독 할당량을 나타내지 않는다. 0-GC 증명에 사용할 수 없다.

Markdown은 3회 결과의 열별 중앙값이다. 개별 실행의 p99 중앙값은 모든 실행의 샘플을
합친 p99가 아니며 신뢰구간도 아니다. 원시 JSON·CSV에는 실행별 값을 보존한다.
CPU ms는 짧은 측정에서 해상도가 낮다. 작은 차이로 우열을 확정하지 않는다.
루프백·동일 프로세스 서버 특성과 장비 응답 지연 부재로 인해 실설비 성능을 대표하지 않는다.

## 실행

저장소 루트의 PowerShell에서 실행한다. 비교 실행 중에는 빌드·테스트·다른 벤치마크를 병행하지 않는다.

```powershell
dotnet test tests/Kable.Tests/Kable.Tests.csproj -c Release --filter FullyQualifiedName~ComparisonTests
dotnet run --project tests/Kable.Comparison.Benchmarks -c Release -- --iterations 100 --warmup 10 --rounds 1 --output artifacts/library-comparison-smoke
dotnet run --project tests/Kable.Comparison.Benchmarks -c Release --no-build -- --iterations 10000 --warmup 200 --rounds 3 --output artifacts/library-comparison
```

출력: `results.md`, `results.csv`, `results.json`, `environment.json`, 라이브러리·실행별 JSON.
환경 파일에는 OS·런타임·CPU 식별자·논리 프로세서 수·GC 모드·패키지 어셈블리 버전을 기록한다.
에코 불일치나 작업 실패는 실행을 실패시키며 순위표에 성공으로 포함하지 않는다.
각 시나리오는 3분의 제한 시간을 가진다. 완료된 정상 실행의 보고서만 비교한다.

## 정확성 검증

`tests/Kable.Tests/Cases/Benchmarks/ComparisonTests.cs`에서 다섯 클라이언트 모두의
분할 응답 복원, 원격 종료 시 대기 실패, 무응답 시 취소, 다중 연결의 실제 작업 수를 검증한다.
추가로 콜백 LF 파서가 한 번에 받은 여러 프레임과 부분 프레임을 올바로 보존하는지 검증한다.

## 다음 비교 범위

CID 다중화, 요청 중 알람·텔레메트리 수신, FIFO 타임아웃 후 늦은 응답,
장시간 메모리 증가, 실제 시리얼·공유 메모리는 이번 측정에 포함하지 않는다.
동일한 장애 정책을 구현하는 비용은 이 정상 경로 결과만으로 판단하지 않는다.

## 2026-10-30 실행 결과

- [전체 비교 표](results/2026-10-30/results.md)
- [135개 실행별 원시 CSV](results/2026-10-30/results.csv)
- [실행 환경과 어셈블리 버전](results/2026-10-30/environment.json)

Release 빌드, .NET 10.0.9 x64, Workstation GC, 논리 프로세서 12개 환경에서
5개 구현 × 9개 시나리오 × 3회, 총 135개 측정을 완료했다.
실제 요청 검증은 총 1,352,160건이며 워밍업 요청은 별도다.
이 결과는 전체 테스트가 끝난 뒤 벤치마크만 별도로 실행한 결과다.
측정기 소스는 `512561f` 커밋이며 빌드는 해당 커밋 직전에 수행했다.
기존 작업 트리의 KableExceptions.cs·KableLocalizer.cs 수정 두 개가 있는 상태로
빌드했으며, 비교 테스트 작업에서는 이 파일들을 변경하지 않았다.
정확성 테스트 21개를 포함한 Kable.Tests 465개가 통과했다.

32바이트·연결 1개에서 관측한 3회 중앙값:

| 구현 | p50 응답 시간 | 초당 요청 | 프로세스 전체 B/op |
| --- | ---: | ---: | ---: |
| .NET 기본 API | 29.3µs | 28,471 | 1,177 |
| Kable | 48.1µs | 17,349 | 3,175 |
| TouchSocket | 55.0µs | 16,694 | 1,072 |
| SuperSocket | 55.8µs | 14,977 | 1,752 |
| DotNetty | 80.6µs | 11,925 | 2,187 |

이 조건에서 Kable은 기본 API 구현보다 느리고 요청당 할당량도 높았다.
다른 세 프레임워크보다 낮은 p50이 관측됐지만 작은 차이의 우열이나 다른 부하까지
일반화하지 않는다. 다중 연결 결과는 전체 표에서 따로 확인해야 한다.
모든 구현이 정상 에코를 수행하는 측정이므로 Kable의 장애 계약이나 폴링·알람 기능의
가치를 평가한 결과는 아니다. 할당량에는 서버·측정 코드도 포함된다.
