namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeGermanTranslations()
    {
        _errorDictionary["de"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Erfolgreich.",
            [KableErrorCode.DeviceDisconnected] = "Hardware-Verbindung wurde getrennt. (Alle ausstehenden Anfragen sofort abgebrochen)",
            [KableErrorCode.DeviceTimeout] = "Befehl '{0}' hat nach {1}ms eine Zeitüberschreitung verursacht.",
            [KableErrorCode.ProtocolViolation] = "Protokollverletzung aufgetreten: {0}",
            [KableErrorCode.InvalidCast] = "Erwarteter Antworttyp '{0}', jedoch '{1}' empfangen.",
            [KableErrorCode.ConnectionFailed] = "Verbindungsaufbau zu '{0}' fehlgeschlagen.",
            [KableErrorCode.OperationCanceled] = "Vorgang wurde abgebrochen.",
            [KableErrorCode.BufferOverflow] = "Pufferkapazität überschritten.",
            [KableErrorCode.HeartbeatLost] = "Heartbeat-Signal verloren. Verbindung als getrennt gewertet.",
            [KableErrorCode.HardwareFault] = "Hardwarefehler erkannt: {0}"
        };
        _stringDictionary["de"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable Kommunikations-Inspektor",
            ["Header_CommandConsole"] = "💬 Aperiodische Befehle",
            ["Header_PeriodicTelemetry"] = "📈 Periodische Telemetrie",
            ["Header_Alarms"] = "🚨 Aktive Alarme",
            ["Header_HexInspector"] = "Hex-Dump / Inspektor",
            ["Header_ManualInjection"] = "Manuelle Befehlseingabe",
            ["Header_DeviceInfo"] = "1. Geräteidentifikation & Beschreibung",
            ["Header_TransportSelection"] = "2. Auswahl der Transportschicht",
            ["Header_CodecRouter"] = "3. Codec & Transaktions-Router",
            ["Header_LiveTest"] = "4. Diagnose & Live-Simulation",
            ["Header_TomlPreview"] = "📄 Automatisch generierte TOML-Vorschau",

            ["Nav_Config"] = "⚙️ Kommunikation (Config)",
            ["Nav_Inspector"] = "💬 Befehle (Aperiodic)",
            ["Nav_Telemetry"] = "📈 Telemetrie (Telemetry)",
            ["Nav_Alarms"] = "🚨 Alarme (Alarms)",
            ["Nav_Export"] = "📄 Exportieren (Export)",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Zeit",
            ["Col_Dir"] = "Richtung",
            ["Col_Device"] = "Gerät",
            ["Col_Type"] = "Typ",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latenz",
            ["Col_PayloadPreview"] = "Nutzdaten-Vorschau",
            ["Col_Parameter"] = "Parameter",
            ["Col_Value"] = "Aktueller Wert",
            ["Col_Unit"] = "Einheit",
            ["Col_Frequency"] = "Frequenz (Hz)",
            ["Col_LastUpdated"] = "Letzte Aktualisierung",
            ["Col_AlarmCode"] = "Alarm-Code",
            ["Col_Severity"] = "Schweregrad",
            ["Col_AlarmMessage"] = "Meldung",
            ["Col_State"] = "Status",

            ["Btn_Send"] = "Senden",
            ["Btn_SendManual"] = "Befehl manuell senden",
            ["Btn_Pause"] = "Pause",
            ["Btn_Resume"] = "Fortsetzen",
            ["Btn_Clear"] = "Löschen",
            ["Btn_Refresh"] = "Aktualisieren",
            ["Btn_RunLiveTest"] = "⚡ Live-Hardwaretest",
            ["Btn_RunSimulation"] = "🔬 Mock-Loopback Simulation",
            ["Btn_SaveConfig"] = "💾 Konfiguration speichern",

            ["Lbl_Filter"] = "Filter:",
            ["Lbl_SelectPacket"] = "Paket auswählen für Hex-Dump...",
            ["Lbl_DeviceId"] = "Geräte-ID:",
            ["Lbl_Description"] = "Beschreibung:",
            ["Lbl_Host"] = "Host-IP:",
            ["Lbl_Port"] = "Port:",
            ["Lbl_SerialPort"] = "COM-Port:",
            ["Lbl_BaudRate"] = "Baudrate:",
            ["Lbl_Parity"] = "Parität:",
            ["Lbl_StopBits"] = "Stoppbits:",
            ["Lbl_DataBits"] = "Datenbits:",
            ["Lbl_PipeName"] = "Pipe-Name:",
            ["Lbl_StatusIdle"] = "Bereit (Kein Test ausgeführt)",
            ["Lbl_StatusTesting"] = "Verbindung wird hergestellt...",
            ["Lbl_StatusSuccess"] = "Verbindung erfolgreich! Latenz: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Verbindungstest fehlgeschlagen: {0}"
        };
    }
}
