namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeJapaneseTranslations()
    {
        _errorDictionary["ja"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "ハードウェア接続が切断されました。（保留中の全リクエストを即時中止）",
            [KableErrorCode.DeviceTimeout] = "コマンド '{0}' の応答が {1}ms 待機後にタイムアウトしました。",
            [KableErrorCode.ProtocolViolation] = "プロトコル違反が発生しました: {0}",
            [KableErrorCode.InvalidCast] = "期待された応答型 '{0}' と実際の型 '{1}' が一致しません。",
            [KableErrorCode.ConnectionFailed] = "'{0}' への接続確立に失敗しました。",
            [KableErrorCode.OperationCanceled] = "操作がキャンセルされました。",
            [KableErrorCode.BufferOverflow] = "バッファ容量の上限を超えました。",
            [KableErrorCode.HeartbeatLost] = "ハートビート信号が途絶えました。接続が切断されたと判断します。",
            [KableErrorCode.HardwareFault] = "ハードウェア障害が検出されました: {0}"
        };
        _stringDictionary["ja"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通信インスペクター",
            ["Header_CommandConsole"] = "💬 随時コマンド通信 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 定常テレメトリ (Periodic)",
            ["Header_Alarms"] = "🚨 リアルタイム警報 (Alarms)",
            ["Header_HexInspector"] = "16進ダンプ & 詳細インスペクター",
            ["Header_ManualInjection"] = "手動コマンド送信バー",
            ["Header_DeviceInfo"] = "1. デバイス識別および説明",
            ["Header_TransportSelection"] = "2. 通信トランスポートの選択",
            ["Header_CodecRouter"] = "3. パケットCodec & トランザクションルーター",
            ["Header_LiveTest"] = "4. 現地通信診断 & シミュレーション",
            ["Header_TomlPreview"] = "📄 自動生成された TOML 設定プレビュー",

            ["Nav_Config"] = "⚙️ 通信設定 (Config)",
            ["Nav_Inspector"] = "💬 随時コマンド (Aperiodic)",
            ["Nav_Telemetry"] = "📈 定常テレメトリ (Telemetry)",
            ["Nav_Alarms"] = "🚨 リアルタイム警報 (Alarms)",
            ["Nav_Export"] = "📄 設定エクスポート (Export)",

            ["Col_Seq"] = "連番(#)",
            ["Col_Time"] = "時刻",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "装置ID",
            ["Col_Type"] = "種別",
            ["Col_Bytes"] = "バイト",
            ["Col_Latency"] = "遅延",
            ["Col_PayloadPreview"] = "ペイロード概要",
            ["Col_Parameter"] = "パラメータ名",
            ["Col_Value"] = "現在値",
            ["Col_Unit"] = "単位",
            ["Col_Frequency"] = "更新レート(Hz)",
            ["Col_LastUpdated"] = "最終更新",
            ["Col_AlarmCode"] = "警報コード",
            ["Col_Severity"] = "重要度",
            ["Col_AlarmMessage"] = "アラーム内容",
            ["Col_State"] = "状態",

            ["Btn_Send"] = "送信",
            ["Btn_SendManual"] = "手動コマンド送信",
            ["Btn_Pause"] = "一時停止",
            ["Btn_Resume"] = "再開",
            ["Btn_Clear"] = "ログ消去",
            ["Btn_Refresh"] = "更新",
            ["Btn_RunLiveTest"] = "⚡ 実機接続診断 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 オフライン模擬 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 設定保存および TOML 生成",

            ["Lbl_Filter"] = "絞り込み:",
            ["Lbl_SelectPacket"] = "パケットを選択すると16進ダンプが表示されます...",
            ["Lbl_DeviceId"] = "デバイス識別子:",
            ["Lbl_Description"] = "デバイス説明:",
            ["Lbl_Host"] = "ホスト IP:",
            ["Lbl_Port"] = "ポート番号:",
            ["Lbl_SerialPort"] = "COMポート:",
            ["Lbl_BaudRate"] = "ボーレート:",
            ["Lbl_Parity"] = "パリティ:",
            ["Lbl_StopBits"] = "ストップビット:",
            ["Lbl_DataBits"] = "データビット:",
            ["Lbl_PipeName"] = "パイプ名:",
            ["Lbl_StatusIdle"] = "待機中 (通信テスト未実施)",
            ["Lbl_StatusTesting"] = "接続およびループバック Ping 実行中...",
            ["Lbl_StatusSuccess"] = "通信接続を確認しました！遅延: {0:F2} ms",
            ["Lbl_StatusFailed"] = "通信診断に失敗しました: {0}"
        };
    }
}
