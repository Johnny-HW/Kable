<!-- Kable Master Documentation Index (German) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 MASTER-DOKUMENTATIONSINDEX</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable Hauptindex (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Zentrales Dokumentationsverzeichnis und Spezifikationsübersicht der reaktiven Kable Hardware-Kommunikations-Engine.
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Bereich</th>
<th style="padding: 12px 16px; font-weight: 700;">Dokumenttitel</th>
<th style="padding: 12px 16px; font-weight: 700;">Behandelte Schwerpunkte</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Erste Schritte</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/README" style="color: #2563eb; text-decoration: none;">Überblick & Schnellstart</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet-Pakete, KableClientBuilder, KableSimple-Fassade, DI-Container</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Erste Schritte</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">Projektspezifikation (Spec)</a></td>
<td style="padding: 12px 16px; color: #475569;">Single Source of Truth (SSOT), 4 Kernentscheidungen & Invarianten</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architektur</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. Architekturüberblick</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3-Schichten-Modell und Klassendiagramm</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architektur</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. Kernschnittstellen-Spezifikation</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> Verträge</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Architektur</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. Implementierungslayout</a></td>
<td style="padding: 12px 16px; color: #475569;">Verzeichnisstruktur, Namensraum-Richtlinien, NuGet-Paketierung</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Zuverlässigkeit</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. Observability & Logging</a></td>
<td style="padding: 12px 16px; color: #475569;">Drei-Kanal-Ringpuffer (<code>DropOldest</code>) & garantierte 60 FPS UI</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Zuverlässigkeit</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. Industrielle Prüfsummen</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, XOR BCC 0-GC Nachschlagetabellen</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Zuverlässigkeit</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. Industrielle Roadmap</a></td>
<td style="padding: 12px 16px; color: #475569;">13 Industrieprotokolle, Determinismusklassen & Integrationsgrenzen</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Standards</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/CONVENTIONS" style="color: #d97706; text-decoration: none;">Coding-Konventionen</a></td>
<td style="padding: 12px 16px; color: #475569;">Zeilengrenzen (300-500), Verbot synchroner Blockaden, TDD-Vorgaben</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Standards</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/de/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. Open-Source-Lizenzierung</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft, permissive Lizenzmatrix (Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>