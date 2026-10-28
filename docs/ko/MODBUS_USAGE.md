# Modbus TCP·RTU 및 PLC 값 변환

현재 저장소 구현 기준이며 NuGet 게시 여부와 별개다. Kable.Modbus는 .NET 10을 대상으로 한다.

## 지원 기능

| 기능 코드 | TCP / RTU Master API |
| --- | --- |
| FC01 | ReadCoilsAsync |
| FC02 | ReadDiscreteInputsAsync |
| FC03 | ReadHoldingRegistersAsync |
| FC04 | ReadInputRegistersAsync |
| FC05 | WriteSingleCoilAsync |
| FC06 | WriteSingleRegisterAsync |
| FC15 (0x0F) | WriteMultipleCoilsAsync |
| FC16 (0x10) | WriteMultipleRegistersAsync |

주소는 프로토콜의 **0부터 시작하는 오프셋**이다. 장비 매뉴얼의 40001 표기를 그대로 전달하지 않는다.
읽기 상한은 Bit 2000개, Register 125개다. 다중 쓰기 상한은 Coil 1968개, Register 123개다.
주소와 수량의 합은 65536 이하여야 한다. Coil은 각 바이트의 최하위 비트부터 포장한다.
응답의 Unit ID·Function·Protocol·Byte Count·쓰기 ACK를 검증하며 잘못된 응답을 성공으로 처리하지 않는다.

기존 IModbusMaster는 변경하지 않았다. 새 Coil 기능을 추상화로 사용하려면 IModbusCoilMaster를 사용한다.
ModbusTcpMaster와 ModbusRtuMaster는 모두 IModbusCoilMaster를 구현한다.

## TCP

```csharp
using Kable.Engine;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Transports;

var session = new KableSession<ModbusTcpMessage>(
    new TcpConnectionFactory("192.168.0.10", 502, connectTimeoutMs: 5000),
    new ModbusTcpCodec());
await using var plc = new ModbusTcpMaster(session, new ModbusTcpOptions
{
    DefaultUnitId = 1,
    Timeout = TimeSpan.FromSeconds(2)
});
await plc.StartAsync();
bool[] coils = await plc.ReadCoilsAsync(0, 16);
await plc.WriteMultipleCoilsAsync(20, new[] { true, false, true });
```

Master가 session을 소유하며 DisposeAsync에서 함께 종료한다.

## RTU / Serial

```csharp
using System.IO.Ports;
using Kable.Modbus.Rtu;

await using var plc = new ModbusRtuMaster(new ModbusRtuOptions
{
    PortName = "COM3",
    BaudRate = 9600,
    DataBits = 8,
    Parity = Parity.Even,
    StopBits = StopBits.One,
    DefaultUnitId = 1,
    Timeout = TimeSpan.FromSeconds(2)
});
await plc.StartAsync();
ushort[] words = await plc.ReadHoldingRegistersAsync(0, 2);
await plc.WriteSingleRegisterAsync(10, 100);
```

- 기본값은 9600bps, 8E1이다. 무패리티를 선택할 때는 StopBits.Two를 사용한다.
- Unit ID는 1..247이다. 응답 없는 브로드캐스트(0)는 지원하지 않는다.
- 하나의 버스는 하나의 Master로 관리한다. 동시에 호출해도 송신부터 응답까지 직렬화된다.
- RTU는 MBAP 없이 Unit·Function·Payload·CRC를 전송한다. 기존 ModbusRtuCodec은 변경하지 않았다.
- 프레임 간 간격은 19200bps 이하에서 3.5문자 시간, 그보다 빠르면 최소 1.75ms다.
  InterFrameDelay로 더 긴 간격을 요구할 수 있다. OS 타이머는 더 늦게 실행될 수 있어 실시간 정밀도를 보장하지 않는다.
- Timeout에는 Master 내부 직렬화 대기·프레임 간 대기·응답 대기가 포함된다.
- 송신 후 타임아웃/취소는 FIFO 세션을 종료한다. 새 Master로 다시 연결해야 한다.
- 송신 전 대기 중 타임아웃/취소는 연결을 유지한다.
- CRC·주소·기능·수량·ACK가 잘못된 응답은 연결을 종료한다. 정상 Modbus 예외 응답은 호출에 오류를 반환하고 연결을 유지한다.
- 자동 재전송하지 않는다. 쓰기 실패 시 장비가 실행했는지 확인하고 다음 동작을 결정한다.
- DisposeAsync는 진행 중 요청과 대기 요청을 취소한다. await using을 사용한다.
- 물리 SerialPort 연결은 기존 전송 계층을 사용한다. RS-485 전기적 방향 전환은 사용 어댑터에서 처리해야 한다.

DI를 사용할 경우 Microsoft.Extensions.DependencyInjection의 AddKableModbusTcp 또는 AddKableModbusRtu를 호출한다.
각 메서드는 IModbusMaster와 IModbusCoilMaster를 같은 인스턴스로 연결한다. 둘을 함께 등록하면 단일 인터페이스 조회에는
마지막 등록이 선택되므로, 복수 장비는 직접 생성하거나 별도의 장비별 등록을 사용한다. RTU는 ModbusRtuMaster로도 조회할 수 있다.

## PLC 자료형 변환

```csharp
using Kable.Modbus.Data;

ushort[] registers = await plc.ReadHoldingRegistersAsync(0, 2);
float value = PlcRegisterConverter.ReadSingle(registers, PlcDataOrder.WordSwap);

ushort[] output = new ushort[2];
PlcRegisterConverter.WriteSingle(12.5f, output, PlcDataOrder.WordSwap);
await plc.WriteMultipleRegistersAsync(10, output);
```

Read/WriteInt32, UInt32, Single은 2개 레지스터, Double은 4개 레지스터를 사용한다.
더 긴 버퍼를 전달하면 숫자는 필요한 앞부분만 사용한다. 목적지의 나머지는 수정하지 않는다.

| 순서 | 0x12345678의 레지스터 배치 |
| --- | --- |
| BigEndian | 1234, 5678 |
| ByteSwap | 3412, 7856 |
| WordSwap | 5678, 1234 |
| LittleEndian | 7856, 3412 |

WordSwap은 Double에서는 4개 워드를 모두 역순으로 배치한다. 장비가 다른 배치를 사용하면 별도 변환이 필요하다.
숫자는 IEEE 754 비트 패턴을 보존하며 Span 기반 변환 자체는 힙 메모리를 할당하지 않는다.

문자열은 ReadString/WriteString으로 **ASCII만** 지원한다. 전체 필드에 순서를 적용한다.
쓰기는 남은 필드를 NUL로 채우고, 읽기는 기본적으로 뒤쪽 NUL만 제거한다. 공백이나 중간 NUL은 보존한다.
용량 초과·비ASCII 문자는 오류이며 쓰기 대상은 변경하지 않는다. 읽기 결과 문자열은 메모리를 할당한다.

```csharp
ushort[] field = new ushort[8];
PlcRegisterConverter.WriteString("DEVICE01", field);
string name = PlcRegisterConverter.ReadString(field);
```

## 검증 범위와 제한

인메모리 실제 세션으로 프레이밍·CRC·예외·취소·시간 초과·단절·직렬화·프레임 간 대기를 검증했다.
물리 COM/RS-485 장비는 검증하지 않았다. 드라이버 버퍼링으로 바이트별 수신 시각을 알 수 없어
프레임 내부 t1.5 문자 간 타임아웃은 검출하지 않는다. 로컬 에코가 있는 어댑터의 에코 제거는 제공하지 않는다.
메시지 객체·배열과 비동기 요청은 할당하므로 전체 통신을 무할당이라고 해석하지 않는다.

규격 기준: [Modbus Organization 공식 사양 목록](https://www.modbus.org/modbus-specifications)의
Application Protocol V1.1b3 및 Serial Line Protocol and Implementation Guide V1.02.
