namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeChineseSimplifiedTranslations()
    {
        _errorDictionary["zh-CN"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "硬件连接已断开。（快速中止所有挂起的请求）",
            [KableErrorCode.DeviceTimeout] = "命令 '{0}' 在 {1}ms 后响应超时。",
            [KableErrorCode.ProtocolViolation] = "发生协议违规: {0}",
            [KableErrorCode.InvalidCast] = "预期响应类型为 '{0}'，但实际接收到 '{1}'。",
            [KableErrorCode.ConnectionFailed] = "无法与 '{0}' 建立连接。",
            [KableErrorCode.OperationCanceled] = "操作已取消。",
            [KableErrorCode.BufferOverflow] = "超出缓冲区容量。",
            [KableErrorCode.HeartbeatLost] = "心跳信号丢失，连接判定中断。",
            [KableErrorCode.HardwareFault] = "检测到硬件故障: {0}"
        };
        _stringDictionary["zh-CN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通信检测器",
            ["Header_CommandConsole"] = "💬 非周期性命令 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 周期性遥测 (Periodic)",
            ["Header_Alarms"] = "🚨 实时报警 (Alarms)",
            ["Header_HexInspector"] = "十六进制报文分析器",
            ["Header_ManualInjection"] = "手动命令发送栏",
            ["Header_DeviceInfo"] = "1. 设备标识与描述",
            ["Header_TransportSelection"] = "2. 通信传输层 (Transport) 选择",
            ["Header_CodecRouter"] = "3. 编解码器与路由配置",
            ["Header_LiveTest"] = "4. 现场通信诊断与模拟",
            ["Header_TomlPreview"] = "📄 自动生成的 TOML 配置预览",

            ["Nav_Config"] = "⚙️ 通信配置 (Config)",
            ["Nav_Inspector"] = "💬 非周期命令 (Aperiodic)",
            ["Nav_Telemetry"] = "📈 实时遥测 (Telemetry)",
            ["Nav_Alarms"] = "🚨 实时报警 (Alarms)",
            ["Nav_Export"] = "📄 导出配置 (Export)",

            ["Col_Seq"] = "序号(#)",
            ["Col_Time"] = "时间",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "设备ID",
            ["Col_Type"] = "报文类型",
            ["Col_Bytes"] = "字节",
            ["Col_Latency"] = "延迟",
            ["Col_PayloadPreview"] = "负载预览",
            ["Col_Parameter"] = "参数名称",
            ["Col_Value"] = "当前值",
            ["Col_Unit"] = "单位",
            ["Col_Frequency"] = "刷新率(Hz)",
            ["Col_LastUpdated"] = "更新时间",
            ["Col_AlarmCode"] = "报警代码",
            ["Col_Severity"] = "严重级别",
            ["Col_AlarmMessage"] = "报警信息",
            ["Col_State"] = "状态",

            ["Btn_Send"] = "发送",
            ["Btn_SendManual"] = "发送手动命令",
            ["Btn_Pause"] = "暂停",
            ["Btn_Resume"] = "恢复",
            ["Btn_Clear"] = "清空记录",
            ["Btn_Refresh"] = "刷新",
            ["Btn_RunLiveTest"] = "⚡ 现场实机测试 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 离线模拟 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 保存配置并生成 TOML",

            ["Lbl_Filter"] = "过滤查询:",
            ["Lbl_SelectPacket"] = "选择报文查看十六进制转储...",
            ["Lbl_DeviceId"] = "设备标识:",
            ["Lbl_Description"] = "设备描述:",
            ["Lbl_Host"] = "主机 IP:",
            ["Lbl_Port"] = "端口:",
            ["Lbl_SerialPort"] = "串口号:",
            ["Lbl_BaudRate"] = "波特率:",
            ["Lbl_Parity"] = "校验位:",
            ["Lbl_StopBits"] = "停止位:",
            ["Lbl_DataBits"] = "数据位:",
            ["Lbl_PipeName"] = "命名管道:",
            ["Lbl_StatusIdle"] = "就绪 (未执行测试)",
            ["Lbl_StatusTesting"] = "正在测试连接与回环 Ping...",
            ["Lbl_StatusSuccess"] = "通信连接正常！延迟: {0:F2} ms",
            ["Lbl_StatusFailed"] = "通信诊断失败: {0}"
        };
    }
}
