<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 MOTOR DE COMUNICACIÓN DE HARDWARE INDUSTRIAL</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Documentación de Kable (Español)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Un motor reactivo de comunicación con hardware de ultra alto rendimiento y Zero-Allocation que combina la abstracción de transporte <code>System.IO.Pipelines</code> de Microsoft Bedrock con patrones de interacción RSocket.
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC Pipelines I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Estado Seguro Fail-Fast</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Búfer Circular Tri-Canal</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines Zero-Copy</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Elimina las copias en memoria en sockets y puertos serie utilizando enmarcado vectorial <code>ReadOnlySequence&lt;byte&gt;</code> con presupuestos de asignación inferiores a 1KB/solicitud.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 Enrutador de Transacciones Híbrido</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Los dispositivos ASCII tradicionales sin ID de correlación se serializan de forma segura con un bloqueo FIFO asíncrono, mientras que los protocolos modernos admiten multiplexación Lock-Free.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Política de Seguridad Fail-Fast</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Emite inmediatamente <code>DeviceDisconnectedException</code> al desconectarse el enlace físico sin reintentos ciegos, llevando el hardware a un estado seguro inmediato.
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 Observabilidad Tri-Canal</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        Aísla la telemetría periódica, la consola de comandos y las alarmas espontáneas en búferes circulares delimitados independientes (<code>DropOldest</code>) para evitar congelamientos en la interfaz de usuario.
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 Inicio Rápido para Desarrolladores
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Paso 1. Instalación del Paquete (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# Motor central de comunicación (TCP, Serial, NamedPipe, Códecs, Builder)
dotnet add package Kable

# Paquete de interfaces y contratos de abstracción pura
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      Paso 2. Conexión Mediante Fluent Builder (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        Configure la capa de transporte y el códec de protocolo limpiamente en solo 3 líneas:
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. Construir la instancia de sesión
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // Delimitado por LF
    .Build();

// 2. Iniciar el pipeline asíncrono de E/S
await session.StartAsync();

// 3. Solicitud-Respuesta RPC con límite de tiempo de 3 segundos
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"Respuesta de temperatura: {response}");

// 4. Recibir flujo de telemetría en tiempo real de forma asíncrona
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"Paquete de telemetría: {packet}");
}
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 Mapa de Especificaciones Técnicas
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Documento</th>
<th style="padding: 12px 16px; font-weight: 700;">Especificación Principal y Alcance</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">Índice maestro de documentación y 4 decisiones arquitectónicas confirmadas</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. Resumen de Arquitectura</a></td>
<td style="padding: 12px 16px; color: #475569;">Capa de transporte Bedrock Pipelines, modelo de interacción RSocket y diagramas</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. Interfaces Principales</a></td>
<td style="padding: 12px 16px; color: #475569;">Contratos API de <code>IDeviceSession</code>, <code>IProtocolCodec</code> y <code>IConnectionContext</code></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. Observabilidad y Registro</a></td>
<td style="padding: 12px 16px; color: #475569;">Búferes circulares delimitados (<code>DropOldest</code>) y 60 FPS garantizados</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. Estructura de Implementación</a></td>
<td style="padding: 12px 16px; color: #475569;">Jerarquía de directorios del repositorio, espacios de nombres y empaquetado</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. Sumas de Verificación Industriales</a></td>
<td style="padding: 12px 16px; color: #475569;">Tablas de búsqueda Zero-Allocation para CRC-16 (Modbus/CCITT), LRC y XOR BCC</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. Hoja de Ruta de Comunicación</a></td>
<td style="padding: 12px 16px; color: #475569;">Espectro de 13 protocolos industriales, clases de determinismo y hoja de ruta</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/es/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. Licenciamiento de Código Abierto</a></td>
<td style="padding: 12px 16px; color: #475569;">Garantía Zero-Copyleft, matriz permisiva (Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>
