namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeChineseTraditionalTranslations()
    {
        _errorDictionary["zh-TW"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "硬體連線已中斷。（快速中止所有擱置的請求）",
            [KableErrorCode.DeviceTimeout] = "指令 '{0}' 在 {1}ms 後回應逾時。",
            [KableErrorCode.ProtocolViolation] = "發生通訊協定違規: {0}",
            [KableErrorCode.InvalidCast] = "預期回應類型為 '{0}'，但實際接收為 '{1}'。",
            [KableErrorCode.ConnectionFailed] = "無法與 '{0}' 建立連線。",
            [KableErrorCode.OperationCanceled] = "操作已取消。",
            [KableErrorCode.BufferOverflow] = "超出緩衝區容量。",
            [KableErrorCode.HeartbeatLost] = "心跳訊號遺失，判定連線中斷。",
            [KableErrorCode.HardwareFault] = "偵測到硬體故障: {0}"
        };
        _stringDictionary["zh-TW"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通訊檢測器",
            ["Header_CommandConsole"] = "💬 非週期性指令 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 週期性遙測 (Periodic)",
            ["Header_Alarms"] = "🚨 即時警報 (Alarms)",
            ["Header_HexInspector"] = "十六進位封包檢索器",
            ["Header_ManualInjection"] = "手動指令注入",
            ["Header_DeviceInfo"] = "1. 設備識別與描述",
            ["Header_TransportSelection"] = "2. 通訊線路 (Transport) 選擇",
            ["Header_CodecRouter"] = "3. 編解碼器與交易路由",
            ["Header_LiveTest"] = "4. 現狀診斷與即時模擬",
            ["Header_TomlPreview"] = "📄 自動產生之 TOML 設定預覽",

            ["Nav_Config"] = "⚙️ 通訊設定 (Config)",
            ["Nav_Inspector"] = "💬 隨機指令 (Aperiodic)",
            ["Nav_Telemetry"] = "📈 遙測數據 (Telemetry)",
            ["Nav_Alarms"] = "🚨 即時警報 (Alarms)",
            ["Nav_Export"] = "📄 匯出設定 (Export)",

            ["Col_Seq"] = "序號(#)",
            ["Col_Time"] = "時間",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "設備ID",
            ["Col_Type"] = "封包類別",
            ["Col_Bytes"] = "位元組",
            ["Col_Latency"] = "延遲",
            ["Col_PayloadPreview"] = "載荷預覽",
            ["Col_Parameter"] = "參數名稱",
            ["Col_Value"] = "當前數值",
            ["Col_Unit"] = "單位",
            ["Col_Frequency"] = "更新頻率(Hz)",
            ["Col_LastUpdated"] = "最後更新",
            ["Col_AlarmCode"] = "警報代碼",
            ["Col_Severity"] = "嚴重度",
            ["Col_AlarmMessage"] = "警報內容",
            ["Col_State"] = "狀態",

            ["Btn_Send"] = "送出",
            ["Btn_SendManual"] = "發送手動指令",
            ["Btn_Pause"] = "暫停",
            ["Btn_Resume"] = "繼續",
            ["Btn_Clear"] = "清空記錄",
            ["Btn_Refresh"] = "重新整理",
            ["Btn_RunLiveTest"] = "⚡ 實機連線診斷 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 離線模擬 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 儲存設定並產生 TOML",

            ["Lbl_Filter"] = "過濾篩選:",
            ["Lbl_SelectPacket"] = "選取封包以檢視十六進位傾印...",
            ["Lbl_DeviceId"] = "設備代碼:",
            ["Lbl_Description"] = "設備說明:",
            ["Lbl_Host"] = "主機 IP:",
            ["Lbl_Port"] = "連接埠:",
            ["Lbl_SerialPort"] = "序列埠:",
            ["Lbl_BaudRate"] = "鮑率:",
            ["Lbl_Parity"] = "同位檢查:",
            ["Lbl_StopBits"] = "停止位元:",
            ["Lbl_DataBits"] = "資料位元:",
            ["Lbl_PipeName"] = "管道名稱:",
            ["Lbl_StatusIdle"] = "待命 (尚未執行連線測試)",
            ["Lbl_StatusTesting"] = "連線與回環 Ping 測試中...",
            ["Lbl_StatusSuccess"] = "連線驗證成功！延遲: {0:F2} ms",
            ["Lbl_StatusFailed"] = "連線診斷失敗: {0}"
        };
    }
}
