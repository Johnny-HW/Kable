# 처음 연결하는 순서

Kable은 .NET용 비동기 장비 통신 라이브러리다. 먼저 문자열 질의를 연결하고,
필요에 따라 주기 조회, 사용자 정의 코덱 또는 장비별 프로토콜로 확장한다.

## 1. 설치하고 한 번 질의하기

기존 .NET 앱의 프로젝트 폴더에서 패키지를 추가한다. 새 앱이라면 먼저 `dotnet new console -n KableDemo`와 `cd KableDemo`를 실행한다.

```bash
dotnet add package Kable
```

다음은 실제 TCP 장비 예제다. 주소·포트·명령을 장비 사양에 맞게 바꾼다.
문자열 예제는 기본적으로 ASCII와 LF(`\n`) 줄 구분자를 사용한다.

```csharp
using Kable.Simple;

await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
string status = await device.QueryAsync("STATUS?");
Console.WriteLine(status);
```

`OpenTcpAsync`는 연결과 내부 수신 처리를 시작한 클라이언트를 반환한다.
추가 `StartAsync` 호출은 필요 없다. `QueryAsync`가 요청을 보내고 응답을 반환하며,
`await using`은 사용 범위가 끝날 때 비동기로 연결을 정리한다.
장비 문서에서 CR(`0x0D`) 같은 다른 구분자를 요구하면 `delimiter`를 명시한다.

다른 전송도 같은 질의 API를 사용한다.

```csharp
using Kable.Simple;

await using var serial = await KableSimple.OpenSerialAsync("COM3", baudRate: 115200);
string model = await serial.QueryAsync("MODEL?");
Console.WriteLine(model);
```

```csharp
using Kable.Simple;

await using var pipe = await KableSimple.OpenNamedPipeAsync("equipment_pipe");
Console.WriteLine(await pipe.QueryAsync("STATUS?"));
```

실물 장비가 없으면 저장소의 로컬 모의 장비 예제를 실행한다.

```bash
dotnet run --project samples/Kable.QuickStart/Kable.QuickStart.csproj
```

## 2. 어떤 API를 선택할까?

| 하려는 작업 | 시작점 | 직접 관리할 내용 |
| --- | --- | --- |
| 문자열 명령을 보내고 한 줄 응답 받기 | `KableSimple` | 명령·주소·응답/이벤트 분류 |
| 상태를 주기적으로 읽고 최신값 보관 | `KableProfileManager` | 조회 간격과 명령 목록 |
| 바이너리 또는 장비 전용 프레임 | `KableClientBuilder<T>`와 코덱 | 메시지 타입·프레이밍·CID 규칙 |
| Modbus / MELSEC 장비 | `Kable.Modbus` / `Kable.Melsec` | 장비 주소와 프로토콜 옵션 |
| 단절 후 새 연결로 복구 | `ReconnectingKableClient` | 재시도 횟수·복구 후 장비 상태 판단 |

Builder는 `Build()` 후 `StartAsync()`가 필요하다. Simple과 Profile의 Open/Connect 메서드는
이미 시작된 클라이언트를 반환한다. 이 세 경로를 한 장비에 모두 구성할 필요는 없다.

## 3. 통신용 Thread나 Task.Run이 필요한가?

일반적인 연결·송수신·질의에는 필요 없다. 내부에서 송신·수신·응답 분배를 비동기로 처리한다.
내부 작업마다 전용 OS 스레드를 하나씩 점유하는 구조는 아니다.
`await`는 작업이 끝날 때까지 호출 스레드를 계속 붙잡아 두지 않는다.
CPU를 오래 사용하는 응답 분석은 애플리케이션이 별도 작업으로 분리할 수 있다.

사용자가 관리하는 Task는 애플리케이션 고유의 반복 작업이다.
반환된 Task를 보관하고 종료 시 취소한 뒤 await한다. 예제의 10초는 실행 범위이며 수신 완료 판정이 아니다.

```csharp
using Kable.Simple;

using var lifetime = new CancellationTokenSource();
await using var device = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
Task polling = PollAsync(device, lifetime.Token);
try
{
    await Task.Delay(TimeSpan.FromSeconds(10));
}
finally
{
    lifetime.Cancel();
    try { await polling; }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
}

static async Task PollAsync(IKableSimpleClient device, CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await timer.WaitForNextTickAsync(ct))
        Console.WriteLine(await device.QueryAsync("STATUS?", ct: ct));
}
```

이 예제는 .NET 8/10용이며, 예상하지 못한 통신 예외는 호출자에게 전달한다.
netstandard2.0 소비자는 취소 가능한 `Task.Delay` 루프나 Profile을 사용한다.

## 4. 주기 조회와 캐시가 필요하면 Profile

```csharp
using Kable.Engine.Profiles;

var config = new KableProfileConfig()
    .UseTcp("192.168.0.100", 9000)
    .AddPeriodic("STATUS?", TimeSpan.FromSeconds(1))
    .AddCommand("Initialize", "INIT");
await using var device = await KableProfileManager.ConnectAsync(config);
Action<string, string> onData = (command, value) => Console.WriteLine($"{command}: {value}");
device.PeriodicDataReceived += onData;
try
{
    Console.WriteLine(await device.ExecuteAsync("Initialize"));
    if (device.TryGetFresh("STATUS?", TimeSpan.FromSeconds(3), out var status))
        Console.WriteLine(status);
}
finally
{
    device.PeriodicDataReceived -= onData;
}
```

실제 장비의 `INIT` 동작을 확인한 뒤 명령을 사용한다. 예제는 명령 별칭의 사용법이다.
첫 폴링 응답 전 `GetLatest`는 null, `TryGetFresh`는 false일 수 있다.
조회 간격만큼 기다렸다고 수신이 끝난 것은 아니다. 초기값이 필수라면 이벤트와 캐시 확인으로
준비 완료를 판정하고 제한시간·단절 처리도 함께 둔다.

## 다음 단계

- [설정과 이벤트 계약](ko/DEVELOPER_USAGE.md): 연결/응답 제한시간, UI 스레드, Builder.
- [실패 처리와 종료](ko/ERROR_HANDLING.md): 예외별 다음 행동, 재연결, 취소.
- [세션 수명 계약](ko/CONNECTION_LIFECYCLE.md): FIFO/CID 차이와 재사용 범위.
- [아키텍처](ko/01_ARCHITECTURE_OVERVIEW.md): 사용자 정의 프로토콜이 필요한 경우.
