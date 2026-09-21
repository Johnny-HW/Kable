<!-- Kable Master Documentation Index (Spanish) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 ÍNDICE MAESTRO DE DOCUMENTACIÓN</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Índice Maestro de Kable (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Índice general de documentación y mapa de especificaciones del motor de comunicación reactiva Kable.
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">Sección</th>
<th style="padding: 12px 16px; font-weight: 700;">Título del Documento</th>
<th style="padding: 12px 16px; font-weight: 700;">Temas Principales</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Primeros Pasos</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/README" style="color: #2563eb; text-decoration: none;">Descripción General & Inicio Rápido</a></td>
<td style="padding: 12px 16px; color: #475569;">Paquetes NuGet, KableClientBuilder, fachada KableSimple e inyección de dependencias</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">Primeros Pasos</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">Especificaciones del Proyecto</a></td>
<td style="padding: 12px 16px; color: #475569;">Única Fuente de Verdad (SSOT), 4 decisiones arquitectónicas e invariantes</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Arquitectura</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. Resumen de Arquitectura</a></td>
<td style="padding: 12px 16px; color: #475569;">Topología en 3 capas Bedrock Pipelines + RSocket y diagramas de clases</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Arquitectura</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. Interfaces Principales</a></td>
<td style="padding: 12px 16px; color: #475569;">Contratos de <code>IConnectionContext</code>, <code>IProtocolCodec</code> y <code>IDeviceSession</code></td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">Arquitectura</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. Estructura de Implementación</a></td>
<td style="padding: 12px 16px; color: #475569;">Árbol de directorios, convenciones de espacios de nombres y empaquetado NuGet</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Fiabilidad</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. Observabilidad y Registro</a></td>
<td style="padding: 12px 16px; color: #475569;">Búferes circulares delimitados (<code>DropOldest</code>) y 60 FPS garantizados</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Fiabilidad</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. Sumas de Verificación Industriales</a></td>
<td style="padding: 12px 16px; color: #475569;">Algoritmos Zero-Allocation Modbus CRC-16, CCITT, LRC y XOR BCC</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">Fiabilidad</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. Hoja de Ruta de Comunicación</a></td>
<td style="padding: 12px 16px; color: #475569;">Espectro de 13 protocolos industriales, determinismo y límites de integración</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Gobernanza</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/CONVENTIONS" style="color: #d97706; text-decoration: none;">Convenciones de Código</a></td>
<td style="padding: 12px 16px; color: #475569;">Límites de 300-500 líneas, prohibición de bloqueo síncrono y TDD</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">Gobernanza</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/es/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. Licenciamiento de Código Abierto</a></td>
<td style="padding: 12px 16px; color: #475569;">Garantía Zero-Copyleft y matriz de licencias permisivas (Apache-2.0, MIT, BSD)</td>
</tr>
</tbody>
</table>
</div>

</div>