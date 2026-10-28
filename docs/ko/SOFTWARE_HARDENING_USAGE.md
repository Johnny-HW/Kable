# 실장비 없이 검증한 통신 보강 사용법

2026-10-28 저장소 구현 기준. NuGet 게시와 별개다.
Kable의 Serial/TLS 전송은 netstandard2.0·.NET 8·.NET 10으로 빌드한다.
Modbus·MQTT·OPC UA 확장 패키지는 .NET 8과 .NET 10 대상이다.

## 시리얼 연결과 종료

기존 SerialPortConnectionFactory와 Builder.UseSerialPort를 그대로 사용한다.
연결 전 취소는 포트를 열지 않고, 연결 중 취소는 호출자에게 반환한 뒤 늦게 열린 포트를 정리한다.
SerialPort.Open 자체는 OS의 동기 API라 ThreadPool에서 실행한다. 드라이버 호출을 강제 중단하지는 못한다.
Abort와 Dispose의 상태를 분리해 단절 후에도 스트림·포트·파이프라인 정리를 수행한다.
DisposeAsync를 동시에 여러 번 호출해도 같은 정리 완료를 기다린다.

## 이름별 장비 관리

```csharp
using Kable.Modbus.Devices;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddKableModbusTcpDevice("main-plc", o =>
{
    o.Host = "192.168.0.10";
    o.DefaultUnitId = 1;
});
services.AddKableModbusRtuDevice("temperature", o =>
{
    o.PortName = "COM3";
    o.DefaultUnitId = 2;
});
await using var provider = services.BuildServiceProvider();
var devices = provider.GetRequiredService<ModbusDeviceRegistry>();
var plc = await devices.ConnectAsync("main-plc");
var sensor = await devices.ConnectAsync("temperature");
```

장비 이름은 대소문자를 구분한다. 중복 이름은 Registry 생성 시 거부한다.
장비는 Get/ConnectAsync에서 지연 생성되고, 설정과 연결은 이름마다 독립적이다.
Registry가 생성한 Master의 소유권을 가지므로 개별 Master를 먼저 Dispose하지 않는다.
Registry 종료 시 한 장비 정리가 실패해도 나머지를 모두 정리하고 AggregateException으로 보고한다.
같은 Master를 여러 이름에 할당하는 것도 거부한다.
사용자 전송/시뮬레이터는 AddKableModbusDevice(name, factory)로 등록한다.

## PLC 태그로 읽고 쓰기

```csharp
using Kable.Modbus.Data;

var temperature = new PlcTag("Temperature", Address: 100, Kind: PlcValueKind.Int16,
    Scale: 0.1, Offset: -20, Unit: "C");
var tags = new PlcTagClient(plc);
PlcTagValue sample = await tags.ReadAsync(temperature);
Console.WriteLine($"{sample.Value} {sample.Tag.Unit}");
await tags.WriteAsync(temperature, 25.0);
```

UInt16/Int16/UInt32/Int32/Single/Double/Ascii를 지원한다.
Order로 기존 네 가지 바이트·워드 순서를 지정한다. Area.Input은 읽기 전용이다.
ASCII는 StringRegisters로 필드 길이를 정하고 배율을 적용하지 않는다.
숫자 표시값은 `원시값 × Scale + Offset`이며 Value에는 double, RawValue에는 원래 자료형을 반환한다.
쓰기는 역변환하며 정수로 표현할 수 없는 값·자료형 범위 초과를 거부한다.
주소는 0부터 시작하는 프로토콜 오프셋이다. 전체 통신과 태그 결과 객체는 무할당이 아니다.

## TCP TLS와 mTLS

```csharp
using Kable.Codecs;
using Kable.Extensions;
using Kable.Transports.Tls;

var options = new TlsClientOptions("plc.example.com");
// mTLS 사용 시 애플리케이션이 로드한 개인 키 포함 인증서를 추가한다.
// options.ClientCertificates.Add(clientCertificate);
await using var session = new KableClientBuilder<string>()
    .UseTlsTcp("plc.example.com", 9000, options, connectTimeoutMs: 5000)
    .UseCodec(new AsciiLineCodec())
    .Build();
await session.StartAsync();
```

기본적으로 인증서 신뢰 체인·호스트명·폐기 상태를 검증한다. TargetHost는 인증서에 기재된 서버 이름이다.
TLS 버전은 OS의 안전한 기본 선택을 사용한다. 인증서를 무조건 허용하는 콜백을 설정하지 않는다.
연결 제한시간에는 TCP 연결과 TLS 핸드셰이크를 포함한다.
서버에서는 TcpListener로 받은 TcpClient를 TlsConnectionContext.AcceptAsync(client, serverOptions, ct)에 전달한다.
TlsServerOptions.Certificate에는 개인 키 포함 서버 인증서가 필요하다.
RequireClientCertificate=true이면 mTLS를 요구한다. 기본 검증은 신뢰할 수 있는 클라이언트 인증서만 허용한다.
컨텍스트는 연결을 소유하고 인증서 객체의 소유권은 호출자에게 남긴다.

## MQTT 구독과 재구독

```csharp
using Kable.Mqtt.Subscriptions;
using MQTTnet;
using MQTTnet.Client;

var options = new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883).WithClientId("equipment-monitor").Build();
await using var subscriber = new MqttSubscriber(options,
    new[] { new MqttTopicFilterBuilder().WithTopic("equipment/+/state").Build() }, capacity: 1024);
await subscriber.StartAsync();
await foreach (var message in subscriber.Messages.ReadAllAsync(cancellationToken))
    Console.WriteLine($"{message.Topic}: {message.Payload.Length} bytes");
```

최초 연결 실패는 StartAsync 호출자에게 반환한다. 성공 후에는 정해진 간격으로 연결을 확인하고 재접속·재구독한다.
IsConnected는 토픽 등록까지 완료되었는지를 나타낸다. 복구 오류는 LastError에서 확인한다.
UnsubscribeAsync는 필터를 제거하고 이후 재연결에서도 복원하지 않는다.
수신 데이터는 소유 배열로 복사한다. 소비가 늦어 큐가 꽉 차면 **새 메시지**를 버리고 DroppedMessages를 증가시킨다.
QoS는 MQTT 전송 수준이다. 이 로컬 큐가 메시지를 영구 보관하거나 exactly-once 업무 처리를 보장하지 않는다.
DisposeAsync는 복구 루프를 취소하고 클라이언트와 수신 채널을 종료한다.

## OPC UA 구독과 일괄 처리

```csharp
using Kable.OpcUa;
using Kable.OpcUa.Subscriptions;

await using var bridge = new OpcUaClientBridge(new OpcUaOptions
{
    EndpointUrl = "opc.tcp://localhost:4840",
    PkiRootPath = @"C:\EquipmentApp\pki"
});
await bridge.ConnectAsync(cancellationToken);
var snapshot = await bridge.ReadNodesAsync(new[] { "ns=2;s=Temperature", "ns=2;s=Pressure" }, cancellationToken);
await using var subscription = await bridge.SubscribeAsync(new[] { "ns=2;s=Temperature" },
    new OpcUaSubscriptionOptions { PublishingIntervalMs = 250, SamplingIntervalMs = 100, Capacity = 1024 }, cancellationToken);
await foreach (var value in subscription.Values.ReadAllAsync(cancellationToken))
    Console.WriteLine($"{value.NodeId}: {value.Value} / quality={value.StatusCode:X8}");
```

ReadNodesAsync는 노드별 값과 품질을 반환한다. 잘못된 노드도 결과 위치를 유지하며 IsGood=false로 보고한다.
WriteNodesAsync는 입력 순서에 대응하는 개별 상태 코드를 반환한다. 실패한 쓰기를 재시도하지 않는다.
KeepAlive에서 단절이 감지되면 새 세션을 만들고 구독을 복원한다. LastRecoveryError로 복구 오류를 확인한다.
복구 중 값을 놓칠 수 있으며, 재시작 전후 알림의 연속성이나 이력 재생을 보장하지 않는다.
구독별 수신 큐가 가득 차면 새 값을 버리고 DroppedValues를 증가시킨다.
구독 DisposeAsync는 해당 구독을 제거하고, Bridge DisposeAsync는 모든 구독과 세션을 종료한다.
기존 IOpcUaClientBridge는 유지했다. 새 기능은 OpcUaClientBridge의 추가 API다.
Options API는 SignAndEncrypt·Basic256Sha256와 서버 인증서 검증이 기본이다.
기존 문자열 생성자는 익명·비보안 호환 경로다. 신뢰 저장소와 사용자 인증 설정은
[설치·보안·업그레이드 안내](RELEASE_GUIDE.md)를 따른다.

## 자동 검증과 실장비 경계

로컬 TCP/TLS, MQTTnet 브로커, OPC Foundation 서버와 인메모리 RTU를 사용한다.
잘못된 인증서·노드·응답, 취소·단절·서버 재시작·수신 큐 포화·지연과 다중 장비 부하를 검증한다.
장시간 테스트는 KABLE_SOAK_SECONDS로 실행 시간을 조절한다. 실제 실행 시간과 결과는 개발 결과 문서에 기록한다.
이는 USB/RS-485 드라이버, 실제 탈착·배선·전기적 노이즈, 24시간 이상의 현장 운전 검증을 대신하지 않는다.
