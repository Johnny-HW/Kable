namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeSpanishTranslations()
    {
        _errorDictionary["es"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Éxito.",
            [KableErrorCode.DeviceDisconnected] = "La conexión de hardware se ha desconectado. (Abortando solicitudes pendientes)",
            [KableErrorCode.DeviceTimeout] = "El comando '{0}' agotó el tiempo de espera tras {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Violación de protocolo detectada: {0}",
            [KableErrorCode.InvalidCast] = "Se esperaba el tipo de respuesta '{0}', pero se recibió '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Error al establecer conexión con '{0}'.",
            [KableErrorCode.OperationCanceled] = "La operación fue cancelada.",
            [KableErrorCode.BufferOverflow] = "Capacidad del búfer superada.",
            [KableErrorCode.HeartbeatLost] = "Señal de latido perdida. Conexión declarada muerta.",
            [KableErrorCode.HardwareFault] = "Falla de hardware detectada: {0}"
        };
        _stringDictionary["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Inspector de Comunicación Kable",
            ["Header_CommandConsole"] = "💬 Comandos Aperiódicos",
            ["Header_PeriodicTelemetry"] = "📈 Telemetría Periódica",
            ["Header_Alarms"] = "🚨 Alarmas Activas",
            ["Header_HexInspector"] = "Volcado Hexadecimal / Inspector",
            ["Header_ManualInjection"] = "Inyección Manual de Comandos",
            ["Header_DeviceInfo"] = "1. Identificación y Descripción del Dispositivo",
            ["Header_TransportSelection"] = "2. Selección de Capa de Transporte",
            ["Header_CodecRouter"] = "3. Códec y Enrutador de Transacciones",
            ["Header_LiveTest"] = "4. Diagnóstico y Simulación en Vivo",
            ["Header_TomlPreview"] = "📄 Vista Previa de TOML Autogenerado",

            ["Nav_Config"] = "⚙️ Configuración (Config)",
            ["Nav_Inspector"] = "💬 Comandos (Aperiodic)",
            ["Nav_Telemetry"] = "📈 Telemetría (Telemetry)",
            ["Nav_Alarms"] = "🚨 Alarmas (Alarms)",
            ["Nav_Export"] = "📄 Exportar (Export)",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Hora",
            ["Col_Dir"] = "Dir",
            ["Col_Device"] = "Dispositivo",
            ["Col_Type"] = "Tipo",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latencia",
            ["Col_PayloadPreview"] = "Vista de Carga Útil",
            ["Col_Parameter"] = "Parámetro",
            ["Col_Value"] = "Valor Actual",
            ["Col_Unit"] = "Unidad",
            ["Col_Frequency"] = "Frecuencia (Hz)",
            ["Col_LastUpdated"] = "Última Actualización",
            ["Col_AlarmCode"] = "Código de Alarma",
            ["Col_Severity"] = "Severidad",
            ["Col_AlarmMessage"] = "Mensaje",
            ["Col_State"] = "Estado",

            ["Btn_Send"] = "Enviar",
            ["Btn_SendManual"] = "Enviar Comando Manual",
            ["Btn_Pause"] = "Pausar",
            ["Btn_Resume"] = "Reanudar",
            ["Btn_Clear"] = "Limpiar",
            ["Btn_Refresh"] = "Actualizar",
            ["Btn_RunLiveTest"] = "⚡ Prueba en Vivo de Hardware",
            ["Btn_RunSimulation"] = "🔬 Simulación Mock Loopback",
            ["Btn_SaveConfig"] = "💾 Guardar Configuración",

            ["Lbl_Filter"] = "Filtro:",
            ["Lbl_SelectPacket"] = "Seleccione un paquete para ver el volcado hexadecimal...",
            ["Lbl_DeviceId"] = "ID de Dispositivo:",
            ["Lbl_Description"] = "Descripción:",
            ["Lbl_Host"] = "IP del Host:",
            ["Lbl_Port"] = "Puerto:",
            ["Lbl_SerialPort"] = "Puerto COM:",
            ["Lbl_BaudRate"] = "Tasa de Baudios:",
            ["Lbl_Parity"] = "Paridad:",
            ["Lbl_StopBits"] = "Bits de Parada:",
            ["Lbl_DataBits"] = "Bits de Datos:",
            ["Lbl_PipeName"] = "Nombre de Canalización:",
            ["Lbl_StatusIdle"] = "Inactivo (Sin prueba ejecutada)",
            ["Lbl_StatusTesting"] = "Conectando y ejecutando ping...",
            ["Lbl_StatusSuccess"] = "¡Comunicación verificada! Latencia: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Fallo en prueba de comunicación: {0}"
        };
    }
}
