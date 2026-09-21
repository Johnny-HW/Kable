<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 HOCHZUVERLÄSSIGE HARDWARE-KOMMUNIKATIONS-ENGINE</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable Dokumentation (Deutsch)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Eine extrem performante, Zero-Allocation reaktive Hardware-Kommunikations-Engine, die Microsoft Bedrocks <code>System.IO.Pipelines</code> Transportabstraktion mit RSocket-Interaktionsmustern vereint.
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC Pipelines I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast Sicherheitszustand</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Drei-Kanal-Ringpuffer Observability</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines Zero-Copy</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Eliminiert Pufferkopien im Socket- und seriellen Datenverkehr durch vektorisiertes <code>ReadOnlySequence&lt;byte&gt;</code>-Framing mit Allokationen unter 1 KB pro Anfrage.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 Hybrider Transaktions-Router</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Serialisiert traditionelle ASCII-Geräte ohne Korrelations-ID über asynchrone FIFO-Locks, während moderne Protokolle lock-frei im Mikrosekundenbereich multiplexen.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast Sicherheitsrichtlinie</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Löst bei physischem Leitungsabbruch sofort <code>DeviceDisconnectedException</code> ohne blinde Wiederholungen aus und überführt Maschinen direkt in den sicheren Zustand.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 Tri-Stream Observability</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Trennt periodische Telemetrie, Befehlskonsolen und Spontanalarmierungen in unabhängige Ringpuffer (<code>DropOldest</code>), um ein Einfrieren der UI vollständig zu verhindern.
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 Entwickler-Schnellstart (Developer Quick Start)
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Schritt 1. Paketinstallation (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# Kernkommunikations-Engine (TCP, Serial, NamedPipe, Codecs, Client-Builder)
dotnet add package Kable

# Nur reine Abstraktionsschnittstellen
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Schritt 2. Fluent Builder Verbindung (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Konfigurieren Sie Transportschicht und Protokoll-Codec übersichtlich in 3 Zeilen:
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. Session-Instanz aufbauen
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // Trennung durch LF
    .Build();

// 2. Asynchrone I/O-Pipeline starten
await session.StartAsync();

// 3. Request-Response RPC mit 3-Sekunden-Watchdog
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"Temperatur-Antwort: {response}");

// 4. Asynchronen Echtzeit-Telemetriestrom empfangen
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"Telemetriedaten: {packet}");
}
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 Technische Dokumentationsübersicht
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Dokument</th>
<th style="padding: 12px 16px; font-weight: 700;">Hauptthemen & Umfang</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">Hauptindex und 4 bestätigte Architekturentscheidungen</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. Architekturüberblick</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines Transportschicht, RSocket-Muster und Klassendiagramm</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. Kernschnittstellen-Spezifikation</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, <code>IConnectionContext</code> Verträge</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. Observability & Logging</a></td>
<td style="padding: 12px 16px; color: #475569;">Drei-Kanal-Ringpuffer (<code>DropOldest</code>) und garantierte 60 FPS UI</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. Implementierungslayout</a></td>
<td style="padding: 12px 16px; color: #475569;">Repository-Dateistruktur, Namespace-Hierarchie und NuGet-Paketierung</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. Industrielle Prüfsummen</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Allocation CRC-16 (Modbus/CCITT), LRC, XOR BCC Nachschlagetabellen</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. Industrielle Kommunikations-Roadmap</a></td>
<td style="padding: 12px 16px; color: #475569;">Vergleich von 13 Industrieprotokollen, Determinisierungsgrade und Grenzen</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/de/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. Open-Source-Lizenzierung</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft-Garantie, permissive Lizenzmatrix (Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>
