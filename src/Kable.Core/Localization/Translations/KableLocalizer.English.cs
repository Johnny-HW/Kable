namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeEnglishTranslations()
    {
        _errorDictionary["en"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Success.",
            [KableErrorCode.DeviceDisconnected] = "Hardware connection has been disconnected. (Fail-fast aborting all pending requests)",
            [KableErrorCode.DeviceTimeout] = "Command '{0}' timed out after {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Protocol violation occurred: {0}",
            [KableErrorCode.InvalidCast] = "Expected response type '{0}', but received '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Failed to establish connection to '{0}'.",
            [KableErrorCode.OperationCanceled] = "Operation was canceled.",
            [KableErrorCode.BufferOverflow] = "Buffer capacity exceeded limit.",
            [KableErrorCode.HeartbeatLost] = "Heartbeat ping lost. Connection deemed dead.",
            [KableErrorCode.HardwareFault] = "Hardware fault detected: {0}"
        };
        _stringDictionary["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable Comm Inspector",
            ["Header_CommandConsole"] = "💬 Aperiodic Commands",
            ["Header_PeriodicTelemetry"] = "📈 Periodic Telemetry",
            ["Header_Alarms"] = "🚨 Active Alarms",
            ["Header_HexInspector"] = "Hex Dump / Inspector",
            ["Header_ManualInjection"] = "Manual Command Injection",
            ["Header_DeviceInfo"] = "1. Device Identification & Description",
            ["Header_TransportSelection"] = "2. Transport Layer Selection",
            ["Header_CodecRouter"] = "3. Codec & Transaction Router",
            ["Header_LiveTest"] = "4. Diagnostics & Live Simulation",
            ["Header_TomlPreview"] = "📄 Auto-Generated TOML Preview",

            ["Nav_Config"] = "⚙️ Comm Config",
            ["Nav_Packets"] = "📋 Packet Catalog",
            ["Nav_Inspector"] = "💬 Aperiodic Commands",
            ["Nav_Telemetry"] = "📈 Telemetry",
            ["Nav_Alarms"] = "🚨 Alarms",
            ["Nav_Export"] = "📄 Config Export",

            ["Header_PacketCatalog"] = "Hardware Packet Catalog & Traffic Classification",
            ["Lbl_TrafficType"] = "Traffic Type:",
            ["Lbl_Aperiodic"] = "Aperiodic (Command)",
            ["Lbl_Periodic"] = "Periodic (Telemetry)",
            ["Lbl_Alarm"] = "Alarm",
            ["Lbl_IntervalMs"] = "Interval (ms):",
            ["Btn_SimStart"] = "▶️ Start Simulation",
            ["Btn_SimStop"] = "⏹️ Stop Simulation",
            ["Btn_AddPacket"] = "➕ Add Packet",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Time",
            ["Col_Dir"] = "Dir",
            ["Col_Device"] = "Device",
            ["Col_Type"] = "Type",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latency",
            ["Col_PayloadPreview"] = "Payload Preview",
            ["Col_Parameter"] = "Parameter",
            ["Col_Value"] = "Current Value",
            ["Col_Unit"] = "Unit",
            ["Col_Frequency"] = "Update Rate (Hz)",
            ["Col_LastUpdated"] = "Last Updated",
            ["Col_AlarmCode"] = "Alarm Code",
            ["Col_Severity"] = "Severity",
            ["Col_AlarmMessage"] = "Message",
            ["Col_State"] = "State",

            ["Btn_Send"] = "Send",
            ["Btn_SendManual"] = "Send Manual Command",
            ["Btn_Pause"] = "Pause",
            ["Btn_Resume"] = "Resume",
            ["Btn_Clear"] = "Clear",
            ["Btn_Refresh"] = "Refresh",
            ["Btn_RunLiveTest"] = "⚡ Live Hardware Test",
            ["Btn_RunSimulation"] = "🔬 Mock Loopback Simulation",
            ["Btn_SaveConfig"] = "💾 Save Configuration",

            ["Lbl_Filter"] = "Filter:",
            ["Lbl_SelectPacket"] = "Select a packet to inspect hex dump...",
            ["Lbl_DeviceId"] = "Device ID:",
            ["Lbl_Description"] = "Description:",
            ["Lbl_Host"] = "Host IP:",
            ["Lbl_Port"] = "Port:",
            ["Lbl_SerialPort"] = "COM Port:",
            ["Lbl_BaudRate"] = "Baud Rate:",
            ["Lbl_Parity"] = "Parity:",
            ["Lbl_StopBits"] = "Stop Bits:",
            ["Lbl_DataBits"] = "Data Bits:",
            ["Lbl_PipeName"] = "Pipe Name:",
            ["Lbl_StatusIdle"] = "Idle (No test executed)",
            ["Lbl_StatusTesting"] = "Connecting and running loopback ping...",
            ["Lbl_StatusSuccess"] = "Communication verified! Latency: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Communication test failed: {0}",

            // Command Console & Packet Catalog Specific Keys
            ["Header_CommandSubTitle"] = "⚡ Dedicated Aperiodic Command Console",
            ["Header_CommandPresets"] = "🎛️ Command & Parameter Presets",
            ["Lbl_SearchCommands"] = "Search commands (e.g. TEMP, PRESS, FLOW...)",
            ["Lbl_ConvertedPreview"] = "Converted:",
            ["Btn_InjectSend"] = "Inject (Send)",
            ["Lbl_SelectPacketHint"] = "Select a packet from the list to inspect byte details.",
            ["Lbl_ManualSend"] = "Manual Send:",
            ["Btn_ExportLog"] = "💾 Export Log",
            ["Header_HexInspectorByte"] = "🔍 Hex Dump & Byte Inspector",
            ["Header_RawPacketStream"] = "📡 Raw Packet Stream Log",
            ["Lbl_RawPacketHint"] = "Click a packet row above to view full byte inspection.",
            ["Lbl_RegisteredCount"] = "{0} registered",
            ["Btn_StartSim"] = "🔬 Start Virtual Simulator",
            ["Btn_StreamingActive"] = "🟢 Streaming Active",
            ["Btn_RefreshPorts"] = "🔄 Refresh Ports",
            ["Lbl_TomlFooter"] = "Kable Real-Time Multi-Protocol Engine • Verified Profile",
            ["Col_Active"] = "Active",
            ["Col_PacketId"] = "Packet ID",
            ["Col_PacketName"] = "Name",
            ["Col_TrafficKind"] = "Traffic Kind",
            ["Col_Interval"] = "Interval (ms)",
            ["Col_SimulatedBase"] = "Base Value",
            ["Col_PayloadFormat"] = "Payload / Format",
            ["Lbl_PacketCatalogDesc"] = "Define hardware packets, configure Periodic/Aperiodic traffic kinds, and set simulation values."
        };
    }
}
