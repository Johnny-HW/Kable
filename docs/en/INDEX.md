<!-- Kable Master Documentation Index -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 MASTER DOCUMENTATION INDEX</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable Master Index
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Master documentation index and specifications navigation map for the Kable reactive communication engine.
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Section</th>
<th style="padding: 12px 16px; font-weight: 700;">Document Title</th>
<th style="padding: 12px 16px; font-weight: 700;">Key Topics Covered</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Getting Started</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/README" style="color: #2563eb; text-decoration: none;">Overview & Quickstart</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet packages, KableClientBuilder, KableSimple facade, and DI container</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Getting Started</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">Project Specifications</a></td>
<td style="padding: 12px 16px; color: #475569;">Single Source of Truth (SSOT), 4 core architectural decisions, and invariants</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architecture</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. Architecture Overview</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3-tier architecture and integrated class diagrams</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architecture</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. Core Interfaces Spec</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> contracts</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architecture</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. Implementation Layout</a></td>
<td style="padding: 12px 16px; color: #475569;">Repository directory tree, namespace guidelines, and packaging layout</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Reliability</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. Observability & Logging</a></td>
<td style="padding: 12px 16px; color: #475569;">Tri-stream bounded ringbuffers (<code>DropOldest</code>) and 60 FPS UI guarantee</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Reliability</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. Industrial Checksums</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, and XOR BCC 0-GC lookup table algorithms</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Reliability</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. Industrial Comm Roadmap</a></td>
<td style="padding: 12px 16px; color: #475569;">13 industrial protocols spectrum, determinism classes, and roadmap</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Governance</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/CONVENTIONS" style="color: #d97706; text-decoration: none;">Coding Conventions</a></td>
<td style="padding: 12px 16px; color: #475569;">Line limits (300-500), synchronous blocking ban, and TDD requirements</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Governance</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/en/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. Open-Source Licensing</a></td>
<td style="padding: 12px 16px; color: #475569;">Permissive licensing matrix (Apache-2.0, MIT, BSD) and Zero-Copyleft rules</td>
</tr>
</tbody>
</table>
</div>

</div>