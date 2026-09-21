<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 工業級高可靠硬體通訊引擎</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 技術文件 (繁體中文)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      結合微軟 Bedrock 的 <code>System.IO.Pipelines</code> 傳輸層抽象化與 RSocket 響應式互動模式的超高效能、Zero-Allocation 工業級硬體通訊框架。
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC Pipelines I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast 安全狀態</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">三通道可觀測環狀緩衝區</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines 零拷貝</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        完全消除通訊埠與序列埠記憶體拷貝，透過 <code>ReadOnlySequence&lt;byte&gt;</code> 向量加速分幀，確保單次請求分配預算低於 1KB。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 混合事務路由器</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        針對無關聯識別碼的傳統 ASCII/序列埠設備採用非同步 FIFO 鎖序列化防護，對現代協議則支援微秒級 Lock-Free 多工並行處理。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast 快速失敗安全政策</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        實體線路中斷時絕不進行盲目重試，立即向所有等待者派發 <code>DeviceDisconnectedException</code>，促使硬體迅速轉移至安全狀態。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 三通道可觀測環狀緩衝區</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        將週期遙測數據、指令控制台與自發告警進行物理隔離（<code>DropOldest</code>），即便在 100Hz 高頻串流下也能徹底防止 UI 介面卡頓。
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 開發者快速上手 (Developer Quick Start)
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      步驟 1. 安裝套件 (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# 核心通訊引擎 (TCP, Serial, NamedPipe, 編解碼器, 客戶端構建器)
dotnet add package Kable

# 純抽象契約與介面專用
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      步驟 2. Fluent 鏈式建構通訊連線 (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        僅需 3 行程式碼即可乾淨設定傳輸層與協議編解碼器：
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. 建構會話實例
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // LF 換行分隔
    .Build();

// 2. 啟動非同步 I/O 管道
await session.StartAsync();

// 3. 具備 3 秒看門狗超時保護的請求-回應 RPC
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"溫度讀取回應: {response}");

// 4. 即時接收非同步遙測串流
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"遙測封包: {packet}");
}
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 技術規格文件索引地圖
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">規格文件</th>
<th style="padding: 12px 16px; font-weight: 700;">核心內容與範圍</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">整體技術文件主索引及 4 大核心架構決策</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. 架構總覽</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines 傳輸層、RSocket 互動模式與類別圖</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. 核心介面規範</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, <code>IConnectionContext</code> API 契約</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. 可觀測性與日誌</a></td>
<td style="padding: 12px 16px; color: #475569;">三通道有界環狀緩衝區（<code>DropOldest</code>）與 60 FPS 流暢 UI 保證</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. 實作與目錄結構</a></td>
<td style="padding: 12px 16px; color: #475569;">儲存庫目錄體系、命名空間劃分及 NuGet 打包發佈結構</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. 工業級校驗碼指引</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Allocation CRC-16(Modbus/CCITT), LRC, XOR BCC 查表演算法</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. 高可靠度通訊藍圖</a></td>
<td style="padding: 12px 16px; color: #475569;">13 大工業通訊協定光譜、確定性等級與官方整合邊界</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-tw/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. 開源授權指引</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 保證，寬鬆許可證矩陣（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>
