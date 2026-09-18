<!-- Kable Conventions & Strict Rules Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #475569 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(148, 163, 184, 0.18); border: 1px solid rgba(148, 163, 184, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #cbd5e1; margin-bottom: 14px;">
      <span>📏 CODE QUALITY & GOVERNANCE</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Conventions & Strict Rules
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Coding standards, line limits, and zero-allocation guidelines for Kable codebase development and AI pair-programming.
    </p>
    <div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
      <span style="background: #475569; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">300-500 Line Limits</span>
      <span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">No Sync Blocking (.Result)</span>
      <span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero-GC Hotpath</span>
      <span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">TDD Test-First</span>
    </div>
  </div>

  <!-- Strict Prohibitions Box -->
  <div style="background: #fef2f2; border-left: 4px solid #ef4444; border-radius: 0 8px 8px 0; padding: 16px 20px; margin-bottom: 26px;">
    <div style="font-weight: 700; color: #b91c1c; font-size: 13.5px; margin-bottom: 6px;">⛔ Strict Prohibitions (CI/PR Failure)</div>
    <ul style="margin: 0; padding-left: 18px; font-size: 12.5px; color: #7f1d1d; line-height: 1.6;">
      <li><strong>No Synchronous Blocking</strong>: Never use <code>.Result</code>, <code>.Wait()</code>, or <code>.GetAwaiter().GetResult()</code>. All I/O must propagate <code>CancellationToken</code> asynchronously.</li>
      <li><strong>No Swallowed Exceptions</strong>: Empty <code>catch { }</code> blocks are strictly forbidden. Connection drops must trigger fail-fast dispatches immediately.</li>
      <li><strong>No Heavy UI/ORM Dependencies</strong>: The core <code>Kable</code> engine must not depend on UI libraries (WPF, WinForms) or disk storage frameworks.</li>
    </ul>
  </div>

</div>
