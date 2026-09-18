<!-- Kable Open-Source Licensing and Compliance Guide -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

  <!-- Hero Header Banner -->
  <div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #047857 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
    <div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(52, 211, 153, 0.18); border: 1px solid rgba(52, 211, 153, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #34d399; margin-bottom: 14px;">
      <span>⚖️ ZERO-COPYLEFT COMPLIANCE</span>
    </div>
    <h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      07. Open-Source Licensing & Compliance Guide
    </h1>
    <p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Zero-Copyleft governance and permissive open-source license matrix (Apache-2.0, MIT, BSD-3-Clause) ensuring 100% proprietary commercial delivery for semiconductor and automated equipment.
    </p>
    <div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
      <span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Apache-2.0 Framework</span>
      <span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">MIT / BSD Dependencies</span>
      <span style="background: #dc2626; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Zero Copyleft (No GPL)</span>
      <span style="background: #d97706; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Express Patent Grant</span>
    </div>
  </div>

  <!-- Key Pillars -->
  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin-bottom: 28px;">
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #059669;">
      <div style="font-weight: 700; color: #047857; font-size: 14px; margin-bottom: 6px;">🛡️ Zero-Copyleft Guarantee</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">GPL, LGPL, and AGPL dependencies are strictly banned. OEMs are never forced to disclose proprietary recipe algorithms.</div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #0284c7;">
      <div style="font-weight: 700; color: #0369a1; font-size: 14px; margin-bottom: 6px;">📜 Permissive Only</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Only Apache-2.0, MIT, BSD-3-Clause, and MS-PL packages are permitted in Kable core and official extensions.</div>
    </div>
    <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; border-top: 3px solid #d97706;">
      <div style="font-weight: 700; color: #b45309; font-size: 14px; margin-bottom: 6px;">🔒 Express Patent Grant</div>
      <div style="font-size: 12.5px; color: #475569; line-height: 1.5;">Apache-2.0 provides explicit global perpetual patent licenses from contributors (Microsoft, Google, etc.).</div>
    </div>
  </div>

  <!-- Section 1: Dependency License Matrix -->
  <div style="margin-bottom: 32px;">
    <h2 style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 0 0 14px 0; display: flex; align-items: center; gap: 8px;">
      <span style="background: #059669; width: 6px; height: 20px; border-radius: 3px; display: inline-block;"></span>
      1. Core & Ecosystem License Matrix
    </h2>

    <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);">
      <table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13px;">
        <thead>
          <tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
            <th style="padding: 12px 14px; font-weight: 700;">Package Name</th>
            <th style="padding: 12px 14px; font-weight: 700;">Role / Domain</th>
            <th style="padding: 12px 14px; font-weight: 700;">Owner</th>
            <th style="padding: 12px 14px; font-weight: 700;">License</th>
            <th style="padding: 12px 14px; font-weight: 700;">Source Disclosure</th>
          </tr>
        </thead>
        <tbody>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">System.IO.Pipelines</td>
            <td style="padding: 12px 14px;">0-GC transport pipelines</td>
            <td style="padding: 12px 14px;">Microsoft (.NET Foundation)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #059669;">MIT</td>
            <td style="padding: 12px 14px; color: #059669;">None (Safe)</td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
            <td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">System.IO.Ports</td>
            <td style="padding: 12px 14px;">RS-232 / 485 serial communication</td>
            <td style="padding: 12px 14px;">Microsoft (.NET Foundation)</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #059669;">MIT</td>
            <td style="padding: 12px 14px; color: #059669;">None (Safe)</td>
          </tr>
          <tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
            <td style="padding: 12px 14px; font-weight: 700; color: #0284c7;">Kable Core Framework</td>
            <td style="padding: 12px 14px;">Reactive engine & builders</td>
            <td style="padding: 12px 14px;">Kable Authors</td>
            <td style="padding: 12px 14px; font-weight: 600; color: #059669;">Apache-2.0</td>
            <td style="padding: 12px 14px; color: #059669;">None (Safe)</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

</div>
