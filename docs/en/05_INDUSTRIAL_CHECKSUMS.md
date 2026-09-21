<!-- Kable Industrial Checksums & CRC Engine Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>⚡ ZERO-ALLOCATION ALGORITHMS</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      05. Industrial Checksums & CRC Engine
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Ultra-high-performance <strong>zero-heap-allocation checksum and CRC toolkit</strong> for industrial instrumentation (Brooks, Crevis, etc.), serial RS-232/485, and socket communication.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero Heap Allocation</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">ReadOnlySpan&lt;byte&gt;</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Pre-computed LUT O(1)</span>
<span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">SIMD Vectorization</span>
</div>
</div>

<!-- Key Architecture Principles (Cards) -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">🚀 Zero-GC Hotpath</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">All computation, validation, and appending APIs execute on stack-allocated spans with zero GC allocations.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">⚡ Pre-computed LUT</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Leverages 256-entry static lookup tables (<code>static readonly ushort[]</code>) to reduce shift operations into O(1) table references.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #d97706;">
<div style="font-weight: 700; color: #b45309; font-size: 14px; margin-bottom: 6px;">🔧 In-Place Packet Framing</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Writes checksums directly to the end of the transmit buffer without intermediate memory copies.</div>
</div>
</div>

<!-- Section 1: Specifications Matrix Table -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. Supported Algorithms & Specification Matrix
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 14px; font-weight: 700;">Algorithm</th>
<th style="padding: 12px 14px; font-weight: 700;">Polynomial</th>
<th style="padding: 12px 14px; font-weight: 700;">Initial Value</th>
<th style="padding: 12px 14px; font-weight: 700;">XorOut</th>
<th style="padding: 12px 14px; font-weight: 700;">Target Hardware & Protocols</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #0369a1;">CRC-16 Modbus</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0xA001 (Reversed)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0xFFFF</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; color: #334155;">Modbus-RTU instruments (Crevis Remote I/O, FFU, Inverters, Temp Controllers)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">CRC-16 CCITT (XModem)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x1021 (Direct)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x0000</td>
<td style="padding: 12px 14px; color: #334155;">Robot controllers, Wafer Pre-Aligners, and safety firmware transfers</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #059669;">LRC (Longitudinal Redundancy)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">2's Complement Sum</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">Modbus ASCII protocol (<code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">:</code> header, <code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">CRLF</code> trailer)</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 14px; font-weight: 700; color: #d97706;">XOR Checksum (BCC)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">Bitwise XOR</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">Barcode readers, RFID carrier ID tags, Vacuum gauges (Brooks, etc.)</td>
</tr>
<tr style="background: #ffffff;">
<td style="padding: 12px 14px; font-weight: 700; color: #7c3aed;">Sum8 / 2's Complement</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">Byte Sum (Modulo 256)</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; font-family: monospace; color: #475569;">0x00</td>
<td style="padding: 12px 14px; color: #334155;">Wafer transfer robots and LoadPort ASCII commands (<code style="background:#f1f5f9; padding:2px 5px; border-radius:4px; font-size:12px;">STX ... ETX + CHK</code>)</td>
</tr>
</tbody>
</table>
</div>
</div>

<!-- Section 2: Code Examples -->
<div style="margin-bottom: 32px;">
<h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      2. Zero-Allocation Developer Guide (C#)
</h2>

<!-- Code Block 1 -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 16px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center;">
<span style="font-weight: 700; font-size: 13.5px; color: #0369a1;">① Modbus CRC-16 Calculation & In-Place Framing</span>
<span style="background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">C# 13 / .NET 10</span>
</div>
<div style="padding: 14px 16px;">

```csharp
using Kable.Core.Checksums;

// 1. Pure CRC-16 computation (0-GC stack allocation)
ReadOnlySpan<byte> payload = stackalloc byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
ushort crc = Crc16Modbus.Compute(payload); // 0xC5CD (Low: 0xC5, High: 0xCD)

// 2. Validate incoming packet integrity
bool isValid = Crc16Modbus.Validate(receivedFrame);
if (!isValid)
{
    throw new ProtocolViolationException("Modbus CRC-16 mismatch: frame corrupted.");
}

// 3. In-place frame assembly with 0 heap allocation
Span<byte> txBuffer = stackalloc byte[payload.Length + 2];
payload.CopyTo(txBuffer);
Crc16Modbus.Append(payload, txBuffer); // Appends CRC atomically at the trailing 2 bytes
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
<span style="font-weight: 700; font-size: 13.5px; color: #d97706;">③ Barcode / RFID Reader XOR BCC</span>
<span style="background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">Bitwise XOR</span>
</div>
<div style="padding: 14px 16px;">

```csharp
using Kable.Core.Checksums;

// STX(0x02) + Command + ETX(0x03)
ReadOnlySpan<byte> rfidCmd = stackalloc byte[] { 0x02, 0x30, 0x31, 0x03 };
byte bcc = IndustrialChecksums.ComputeXorBcc(rfidCmd);
```

</div>
</div>
</div>

</div>
