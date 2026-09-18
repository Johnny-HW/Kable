<!-- Kable Industrial High-Reliability Communication Roadmap Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
      <span>🌐 PROTOCOL SPECTRUM & ROADMAP</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      06. High-Reliability Industrial Communication Roadmap
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Comprehensive technical comparison of 13 industrial communication protocols, determinism classifications, and official integration boundaries for Kable.
    </p>
    <div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
      <span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">13 Protocol Spectrum</span>
      <span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Soft vs Hard Real-Time</span>
      <span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">IPC Memory-Mapped Files</span>
      <span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">OPC UA & MQTTnet</span>
    </div>
  </div>

  <!-- Scope Notice Box -->
  <div style="background: #eff6ff; border-left: 4px solid #3b82f6; border-radius: 0 8px 8px 0; padding: 16px 20px; margin-bottom: 26px;">
    <div style="font-weight: 700; color: #1d4ed8; font-size: 13.5px; margin-bottom: 4px;">📌 System Boundary & Scope Definition</div>
    <div style="font-size: 12.5px; color: #334155; line-height: 1.6;">
      <strong>Kable is not a hard real-time motion controller or a certified safety controller.</strong><br/>
      Kable provides ultra-high-performance, zero-allocation mediation for <strong>non-safety equipment communication, observability, and telemetry streaming</strong>. Critical sub-millisecond motion buses (such as EtherCAT and CIP Safety) are integrated through verified external master SDK bridges.
    </div>
  </div>

  <!-- Section 1: Protocols Comparison Matrix -->
  <div style="margin-bottom: 32px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #0284c7; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. Industrial Protocols Comparison Matrix
    </h2>

    <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
      <table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
        <thead>
          <tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
            <th style="padding: 12px 14px; font-weight: 700;">Domain</th>
            <th style="padding: 12px 14px; font-weight: 700;">Protocol / Tech</th>
            <th style="padding: 12px 14px; font-weight: 700;">Determinism Class</th>
            <th style="padding: 12px 14px; font-weight: 700;">License</th>
            <th style="padding: 12px 14px; font-weight: 700;">Kable Support Status</th>
          </tr>
        </thead>
        <tbody>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">IPC (Single OS)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Named Pipe IPC</td>
            <td style="padding: 12px 14px;">Soft Real-Time</td>
            <td style="padding: 12px 14px;">OS Native (Free)</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Built-in (UseNamedPipe)</span></td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">IPC (Single OS)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">MMF SharedQueue / RingBuffer</td>
            <td style="padding: 12px 14px;">Soft Real-Time</td>
            <td style="padding: 12px 14px;">Apache-2.0</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.SharedMemory</span></td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">Network (Remote)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Raw TCP Socket (NoDelay)</td>
            <td style="padding: 12px 14px;">Best Effort</td>
            <td style="padding: 12px 14px;">OS Native (Free)</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Built-in (UseTcp)</span></td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">Network (Remote)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">gRPC (HTTP/2 + Protobuf)</td>
            <td style="padding: 12px 14px;">Soft Real-Time</td>
            <td style="padding: 12px 14px;">Apache-2.0</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.Grpc</span></td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">Fieldbus / PLC</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">RS-232C / RS-485 Serial</td>
            <td style="padding: 12px 14px;">Soft Real-Time</td>
            <td style="padding: 12px 14px;">OS Native (Free)</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Built-in (UseSerialPort)</span></td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">Fieldbus / PLC</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">Modbus-TCP / RTU</td>
            <td style="padding: 12px 14px;">Soft Real-Time</td>
            <td style="padding: 12px 14px;">Apache-2.0</td>
            <td style="padding: 12px 14px;"><span style="background:#dcfce7; color:#15803d; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">✅ Kable.Modbus</span></td>
          </tr>
          <tr style="background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #475569;">Fieldbus / PLC</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #0284c7;">EtherCAT</td>
            <td style="padding: 12px 14px;">Hard Real-Time</td>
            <td style="padding: 12px 14px;">Commercial / GPL</td>
            <td style="padding: 12px 14px;"><span style="background:#fee2e2; color:#b91c1c; padding:2px 8px; border-radius:6px; font-size:11px; font-weight:700;">🚫 Excluded from Kable Core</span></td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

</div>
