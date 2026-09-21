<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 산업용 고성능 하드웨어 통신 엔진</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 기술 문서 (한국어)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      마이크로소프트 Bedrock의 <code>System.IO.Pipelines</code> 전송 계층 추상화와 RSocket 인터랙션 패턴을 결합한 초고성능, Zero-Allocation 반응형 하드웨어 통신 프레임워크입니다.
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC 파이프라인 I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast 안전 상태</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">3채널 관측성 링버퍼</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines Zero-Copy</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        소켓 및 시리얼 메모리 버퍼 복사를 완전히 배제하고, <code>ReadOnlySequence&lt;byte&gt;</code> 벡터 가속 프레이밍을 통해 요청당 1KB 미만의 메모리 할당을 보장합니다.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 하이브리드 트랜잭션 라우터</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        연관 토큰이 없는 레거시 ASCII/시리얼 장비는 비동기 FIFO 락으로 자동 직렬화하고, 최신 프로토콜은 마이크로초 단위의 Lock-Free 다중화 처리를 지원합니다.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast 안전 정책</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        물리 링크 단절 시 맹목적인 재시도 없이 모든 대기자에게 <code>DeviceDisconnectedException</code>을 즉각 전파하여 하드웨어를 즉시 물리적 안전 상태로 전이합니다.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 3채널 관측성 링버퍼</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        주기적 텔레메트리, 커맨드 콘솔, 자발적 알람을 독립된 유한 링버퍼(<code>DropOldest</code>)로 격리하여 초고속 통신 중에도 UI 멈춤 현상을 원천 방지합니다.
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 개발자 퀵스타트 (Developer Quick Start)
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      단계 1. 패키지 설치 (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# 코어 통신 엔진 (TCP, Serial, NamedPipe, 코덱, 클라이언트 빌더)
dotnet add package Kable

# 순수 추상화 계약 및 인터페이스 전용
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      단계 2. Fluent Builder 세션 구축 (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        전송 계층과 프로토콜 코덱을 단 3줄의 코드로 깔끔하게 구성:
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. 세션 인스턴스 빌드
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // LF 기준 구분
    .Build();

// 2. 비동기 I/O 파이프라인 시작
await session.StartAsync();

// 3. 3초 워치독 타임아웃 제한이 적용된 질의-응답 RPC
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"온도 응답: {response}");

// 4. 실시간 비동기 텔레메트리 스트림 수신
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"텔레메트리 패킷: {packet}");
}
```

</div>
</div>

<!-- Step 3: KableSimple Facade -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      단계 3. 초간편 퍼사드 (`KableSimple`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        신속한 하드웨어 프로토타이핑, 간이 진단 및 시퀀스 테스트를 위한 최소형 API:
</p>

```csharp
using Kable.Simple;

// 연결 수립 및 비동기 Dispose 보장
await using var client = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);

// 실시간 수신 이벤트 및 에러 구독
client.LineReceived += line => Console.WriteLine($"[수신] {line}");
client.ErrorOccurred += ex => Console.Error.WriteLine($"[오류] {ex.Message}");

// 단방향 명령 전송 및 질의 실행
await client.SendLineAsync("SERVO:ENABLE");
string status = await client.QueryAsync("SERVO:STATUS?");
```

</div>
</div>

<!-- Step 4: DI Container -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 30px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      단계 4. 의존성 주입 컨테이너 연동 (`Microsoft.Extensions.DependencyInjection`)
</div>
<div style="padding: 14px 18px;">

```csharp
// 1. 관측성 및 멀티채널 링버퍼 서비스 등록
builder.Services.AddKable();

// 2. 타입이 정의된 장비 통신 세션 등록
builder.Services.AddKableSession<string>((client, sp) =>
{
    client.UseSerialPort("COM3", baudRate: 9600)
          .UseCodec(new AsciiLineCodec(delimiter: 0x0D));
});
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 기술 사양 문서 사이트맵
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">사양서 파일</th>
<th style="padding: 12px 16px; font-weight: 700;">핵심 명세 및 범위</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">전체 기술 문서 마스터 인덱스 및 4대 확정 아키텍처 결정사항</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. 아키텍처 개요</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines 전송 계층, RSocket 인터랙션 모델 및 클래스 다이어그램</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. 코어 인터페이스 명세서</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, <code>IConnectionContext</code> 정형 계약</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. 관측성 및 로깅</a></td>
<td style="padding: 12px 16px; color: #475569;">3채널 유한 링버퍼(<code>DropOldest</code>) 및 60 FPS 무중단 UI 보장</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. 구현 및 디렉터리 구조</a></td>
<td style="padding: 12px 16px; color: #475569;">저장소 디렉터리 체계, 네임스페이스 분할 및 NuGet 패키징 배포 레이아웃</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. 산업용 체크섬 가이드</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Allocation CRC-16(Modbus/CCITT), LRC, XOR BCC 룩업 테이블</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. 산업용 통신 로드맵</a></td>
<td style="padding: 12px 16px; color: #475569;">13대 산업용 통신 프로토콜 스펙트럼, 결정론 등급 및 연동 로드맵</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ko/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. 오픈소스 라이선스 가이드</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 보장, 허용적(Permissive) 라이선스 매트릭스(Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>
