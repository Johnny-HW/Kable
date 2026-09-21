<!-- Kable Industrial Checksums & CRC Engine Document (Korean) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>⚡ ZERO-ALLOCATION 고속 알고리즘</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      05. 산업용 체크섬 및 CRC 엔진 (Industrial Checksums & CRC Engine)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      산업용 계측기(Brooks, Crevis 등), 시리얼 RS-232/485 및 소켓 통신을 위한 초고성능 <strong>Zero-Heap-Allocation 체크섬 및 CRC 툴킷</strong>입니다.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero Heap Allocation</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">ReadOnlySpan&lt;byte&gt;</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">사전 계산 LUT O(1)</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">SIMD 벡터 가속</span>
</div>
</div>

<!-- Key Architecture Principles (Cards) -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">🚀 Zero-GC 핫패스 (Hotpath)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">모든 연산, 유효성 검증, 프레임 추가 API는 GC 힙 할당 없이 스택 기반 Span 상에서 완결됩니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">⚡ 사전 계산된 룩업 테이블 (LUT)</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">256개 엔트리의 정적 룩업 테이블(<code>static readonly ushort[]</code>)을 활용하여 비트 시프트 연산을 O(1) 테이블 참조로 축소합니다.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #d97706;">
<div style="font-weight: 700; color: #b45309; font-size: 14px; margin-bottom: 6px;">🔧 인플레이스(In-Place) 프레이밍</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">중간 메모리 복사 없이 송신 버퍼의 끝단에 체크섬을 직접 기록합니다.</div>
</div>
</div>

<!-- Section 1: Specifications Matrix Table -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. 지원 알고리즘 및 사양 매트릭스
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">알고리즘</th>
<th style="padding: 12px 14px; font-weight: 700;">다항식 (Polynomial)</th>
<th style="padding: 12px 14px; font-weight: 700;">초기값 (Initial)</th>
<th style="padding: 12px 14px; font-weight: 700;">XorOut</th>
<th style="padding: 12px 14px; font-weight: 700;">적용 장비 및 프로토콜</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0369a1;">CRC-16 Modbus</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0xA001 (Reversed)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0xFFFF</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; color: #334155;">Modbus-RTU 장비 (Crevis Remote I/O, FFU, 인버터, 온도 조절기)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">CRC-16 CCITT (XModem)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x1021 (Direct)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; color: #334155;">로봇 컨트롤러, 웨이퍼 프리얼라이너 및 안전 펌웨어 전송</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #059669;">LRC (Longitudinal Redundancy)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">2의 보수 합 (2's Complement)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">Modbus ASCII 프로토콜 (<code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">:</code> 헤더, <code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">CRLF</code> 트레일러)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #d97706;">XOR Checksum (BCC)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">비트 단위 XOR</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">바코드 리더기, RFID 캐리어 태그, 진공 게이지 (Brooks 등)</td>
</tr>
<tr style="background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #7c3aed;">Sum8 / 2의 보수</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">바이트 단순 합 (Modulo 256)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">웨이퍼 이송 로봇 및 로드포트(LoadPort) ASCII 명령 (<code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">STX ... ETX + CHK</code>)</td>
</tr>
</tbody>
</table>
</div>
</div>

<!-- Section 2: Code Examples -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      2. Zero-Allocation 실전 C# 예제 가이드
</h2>

<!-- Code Block 1 -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 16px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 13.5px; color: #0369a1;">① Modbus CRC-16 연산 및 인플레이스 패킷 프레이밍</span>
<span style="background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">C# 14 / .NET 10</span>
</div>
<div style="padding: 14px 16px;">

```csharp
using Kable.Core.Checksums;

// 1. 순수 CRC-16 계산 (스택 할당 0-GC)
ReadOnlySpan<byte> payload = stackalloc byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
ushort crc = Crc16Modbus.Compute(payload); // 0xC5CD (Low: 0xC5, High: 0xCD)

// 2. 유입된 수신 패킷의 무결성 검증
bool isValid = Crc16Modbus.Validate(receivedFrame);
if (!isValid)
{
    throw new ProtocolViolationException("Modbus CRC-16 불일치: 프레임이 손상되었습니다.");
}

// 3. 힙 할당 없는 인플레이스 프레임 조립
Span<byte> txBuffer = stackalloc byte[payload.Length + 2];
payload.CopyTo(txBuffer);
Crc16Modbus.Append(payload, txBuffer); // 끝단 2바이트에 CRC를 원자적으로 기록
```

</div>
</div>

<!-- Code Block 2 -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 16px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 13.5px; color: #059669;">② Modbus ASCII LRC</span>
<span style="background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">2's Complement</span>
</div>
<div style="padding: 14px 16px;">

```csharp
using Kable.Core.Checksums;

ReadOnlySpan<byte> asciiData = stackalloc byte[] { 0x01, 0x03, 0x04, 0x02, 0x55 };
byte lrc = IndustrialChecksums.ComputeModbusLrc(asciiData);
```

</div>
</div>

<!-- Code Block 3 -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 13.5px; color: #d97706;">③ 바코드 / RFID 리더기용 XOR BCC</span>
<span style="background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Bitwise XOR</span>
</div>
<div style="padding: 14px 16px;">

```csharp
using Kable.Core.Checksums;

// STX(0x02) + 커맨드 + ETX(0x03)
ReadOnlySpan<byte> rfidCmd = stackalloc byte[] { 0x02, 0x30, 0x31, 0x03 };
byte bcc = IndustrialChecksums.ComputeXorBcc(rfidCmd);
```

</div>
</div>
</div>

</div>
