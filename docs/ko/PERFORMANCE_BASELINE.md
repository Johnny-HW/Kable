# 문자열 API 성능 기준 측정

측정일: 2026-10-01. Release / BenchmarkDotNet 0.14.0.

환경: Windows 11, Intel Core 7 150U, .NET SDK 10.0.301, 런타임 .NET 10.0.9 x64 AVX2, Concurrent Workstation GC. 워밍업 1회, 측정 3회, 반복 목표 시간 100ms, 프로세스 1회.

## 측정 범위

문자열 API 전체는 무할당이 아니다. 현대 .NET에서는 송신 인코딩을 미리 확보된 출력 버퍼에 직접 수행한다. 수신 디코딩은 문자열을 생성한다. netstandard2.0 송신은 배열 풀을 사용하며 초기 풀 확장 비용과 복사가 발생할 수 있다.

이번 측정은 .NET 10에 한정한다. .NET 8·netstandard2.0의 수치, 실장비 지연, 네트워크 성능, 장시간 부하 성능을 대표하지 않는다.

코덱은 초기화 후 출력 버퍼를 재사용하고 단일 세그먼트 입력을 사용했다. 세션 측정은 인메모리 연결과 에코 서버를 포함한다. 할당량에는 테스트 에코 서버와 비동기 작업 비용도 포함되므로 엔진 단독 할당으로 해석하지 않는다.

## 결과

| 코덱 작업 | 문자 수 | 인코딩 | 평균 | 관측된 할당/작업 |
| --- | --- | --- | --- | --- |
| 송신 | 32 | ASCII | 8.630ns | 관측 안 됨 |
| 수신 | 32 | ASCII | 45.738ns | 120B |
| 송신 | 32 | UTF-8 | 43.749ns | 관측 안 됨 |
| 수신 | 32 | UTF-8 | 101.759ns | 120B |
| 송신 | 1024 | ASCII | 40.119ns | 관측 안 됨 |
| 수신 | 1024 | ASCII | 169.926ns | 2104B |
| 송신 | 1024 | UTF-8 | 880.155ns | 관측 안 됨 |
| 수신 | 1024 | UTF-8 | 1812.852ns | 2104B |

UTF-8 입력은 한글 문자 반복이므로 ASCII와 동일한 바이트 수가 아니다.

| 세션 작업 | 문자 수 | 옵저버 | 평균/작업 | 할당/작업 |
| --- | --- | --- | --- | --- |
| 질의·응답 | 32 | 없음 | 32.170µs | 3662B |
| 질의·응답 | 32 | CommObserver | 37.534µs | 3662B |
| 질의·응답 | 1024 | 없음 | 43.164µs | 7660B |
| 질의·응답 | 1024 | CommObserver | 49.300µs | 7639B |
| 스트림 | 32 | 없음 | 0.824µs | 147B |
| 스트림 | 32 | CommObserver | 1.411µs | 160B |
| 스트림 | 1024 | 없음 | 3.143µs | 2234B |
| 스트림 | 1024 | CommObserver | 4.124µs | 2245B |

스트림은 100개 메시지를 묶어서 처리하고 `OperationsPerInvoke=100`으로 메시지당 환산했다. 옵저버는 유한 큐에 기록하며 UI·디스크 출력은 포함하지 않는다. 단회 호출 지연과 동일하지 않다.

측정 횟수가 적고 일부 반복은 목표 시간보다 짧아 BenchmarkDotNet 경고가 발생했다. 지연의 신뢰구간이 넓으므로 옵저버의 성능 차이나 작은 시간 차이를 확정적 결론으로 사용하지 않는다. 할당 패턴과 재현 가능한 초기 기준을 확인하는 용도다.

## 재현

저장소 루트에서 실행한다.

```powershell
dotnet run --project tests/Kable.Benchmarks/Kable.Benchmarks.csproj -c Release -- --filter '*AsciiLineBenchmarks*' --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --launchCount 1 --artifacts ./artifacts/dx-benchmarks
dotnet run --project tests/Kable.Benchmarks/Kable.Benchmarks.csproj -c Release -- --filter '*SessionBenchmarks*' --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --launchCount 1 --artifacts ./artifacts/dx-session-benchmarks
```

안정적인 성능 비교는 프로세스·측정 횟수와 반복 시간을 늘려 수행한다. 원본 측정 요약은 [코덱](benchmarks/AsciiLineBenchmarks.md), [세션](benchmarks/SessionBenchmarks.md)에 보존했다.
