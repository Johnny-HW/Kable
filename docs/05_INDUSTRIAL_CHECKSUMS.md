# Kable Industrial Checksum & CRC Engine Specification

본 문서는 반도체, FPD, 정밀 계측 및 자동화 장비와의 시리얼(RS-232/485) 및 소켓 통신에서 패킷 무결성을 검증하기 위한 **초고성능 0-GC Checksum & CRC 툴킷**의 표준 명세입니다.

`HighPerformance_Architecture_Guide.md`의 Zero-Allocation 원칙에 따라, 모든 연산은 힙 할당 없이 `ReadOnlySpan<byte>` 및 사전 계산된 정적 테이블(LUT)을 통해 O(1)에 준하는 속도로 수행됩니다.

---

## 1. 지원 알고리즘 및 표준 사양

| 알고리즘 | 다항식 (Polynomial) | 초기값 (Init) | 결과 반전 (XorOut) | 주요 적용 하드웨어 및 프로토콜 |
| :--- | :--- | :--- | :--- | :--- |
| **CRC-16 Modbus** | `0xA001` (역방향) | `0xFFFF` | `0x0000` | Modbus-RTU 장비 (Crevis DIO, FFU, 온도 컨트롤러, 인버터) |
| **CRC-16 CCITT (XModem)**| `0x1021` | `0x0000` | `0x0000` | 외산 로봇 암, 웨이퍼 Aligner, 펌웨어 전송 |
| **LRC (Longitudinal Redundancy)**| 2의 보수 합산 | `0x00` | `0x00` | Modbus ASCII 통신 (`:` 헤더, CRLF 테일) |
| **XOR Checksum (BCC)** | Bitwise XOR | `0x00` | `0x00` | 바코드 리더, RFID 리더, 진공 게이지(Brooks 등) |
| **Sum8 / 2's Complement** | Byte Sum | `0x00` | `0x00` | 반도체 웨이퍼 반송 로봇 및 LoadPort ASCII 커맨드 (`STX ... ETX + CHK`) |

---

## 2. API 사용 가이드 (Zero-Allocation)

### ① Modbus CRC-16 계산 및 검증
```csharp
using Kable.Core.Checksums;

// 1. 순수 CRC 계산
ReadOnlySpan<byte> payload = stackalloc byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
ushort crc = Crc16Modbus.Compute(payload); // 0xC5CD (Low: 0xC5, High: 0xCD)

// 2. 패킷 수신 시 무결성 검증 (수신된 꼬리 2바이트 CRC 포함 버퍼 전달)
bool isValid = Crc16Modbus.Validate(receivedFrame);

// 3. 제자리(In-place) 패킷 조립 (힙 할당 0바이트)
Span<byte> txBuffer = stackalloc byte[payload.Length + 2];
payload.CopyTo(txBuffer);
Crc16Modbus.Append(payload, txBuffer); // txBuffer 끝 2바이트에 CRC 자동 기입
```

### ② Modbus ASCII LRC
```csharp
ReadOnlySpan<byte> asciiData = stackalloc byte[] { 0x01, 0x03, 0x04, 0x02, 0x55 };
byte lrc = IndustrialChecksums.ComputeModbusLrc(asciiData);
```

### ③ Barcode / RFID용 XOR BCC
```csharp
ReadOnlySpan<byte> rfidCmd = stackalloc byte[] { 0x02, 0x30, 0x31, 0x03 }; // STX 0 1 ETX
byte bcc = IndustrialChecksums.ComputeXorBcc(rfidCmd);
```
