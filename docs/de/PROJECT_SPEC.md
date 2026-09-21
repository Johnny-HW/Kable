<!-- Kable Project Specification SSOT Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 SINGLE SOURCE OF TRUTH (SSOT)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Project Specification
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Mission statement, architectural topology, and fundamental engineering invariants for the Kable reactive communication engine.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Bedrock Pipelines</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">RSocket Reactive</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Multi-Targeting</span>
<span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Fail-Fast Safety</span>
</div>
</div>

<!-- 4 Core Decisions -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
<div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">1. Hybrid Routing</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Automatic serializing FIFO lock for uncorrupted ASCII devices, plus lock-free multiplexing for modern tokens.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 14px; margin-bottom: 6px;">2. Fail-Fast Safety</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Immediate <code>DeviceDisconnectedException</code> dispatch upon link severed to guarantee physical safe-state.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">3. Strict Separation</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Engine focuses on 0-GC packet routing; disk file logging and UI rendering are decoupled to bounded ringbuffers.</div>
</div>
<div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 14px; margin-bottom: 6px;">4. Multi-Targeting</div>
<div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Native compilation for .NET 10.0, .NET 8.0 LTS, and netstandard2.0 (.NET Framework 4.8 compatible).</div>
</div>
</div>

</div>
