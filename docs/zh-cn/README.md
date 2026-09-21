<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 工业级高可靠硬件通信引擎</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 技术文档 (简体中文)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      融合微软 Bedrock 的 <code>System.IO.Pipelines</code> 传输层抽象与 RSocket 响应式交互模式的超高性能、Zero-Allocation 硬件通信框架。
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC Pipelines I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast 安全状态</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">三通道可观测环形缓冲区</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines 零拷贝</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        彻底消除网络套接字与串口内存拷贝，基于 <code>ReadOnlySequence&lt;byte&gt;</code> 向量加速分帧，确保单次请求分配预算低于 1KB。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 混合事务路由器</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        针对无关联标识的传统 ASCII/串口设备采用异步 FIFO 锁序列化保护，对现代协议则支持微秒级 Lock-Free 多路复用。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast 快速失败安全机制</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        物理链路断开时拒绝盲目重试，立即向所有等待者派发 <code>DeviceDisconnectedException</code>，促使硬件迅速切换至安全停机状态。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 三通道可观测环形缓冲区</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        将周期遥测、指令控制台与自发告警进行物理隔离（<code>DropOldest</code>），即便在 100Hz 高频数据流下也能彻底防止 UI 界面卡死。
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 开发者快速上手 (Developer Quick Start)
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      步骤 1. 安装软件包 (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# 核心通信引擎 (TCP, Serial, NamedPipe, 编解码器, 客户端构建器)
dotnet add package Kable

# 纯抽象接口契约包
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      步骤 2. Fluent 链式构建通信连接 (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        仅需 3 行代码即可清晰配置传输层与协议编解码器：
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. 构建会话实例
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // LF 换行分隔
    .Build();

// 2. 启动异步 I/O 管道
await session.StartAsync();

// 3. 具备 3 秒看门狗超时保护的请求-响应 RPC
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"温度读取响应: {response}");

// 4. 实时接收异步遥测数据流
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"遥测数据包: {packet}");
}
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 技术文档索引地图
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">技术文档</th>
<th style="padding: 12px 16px; font-weight: 700;">核心内容与技术范围</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">整体技术文档主索引及 4 大核心架构决策</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. 架构总览</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines 传输层、RSocket 交互模式与类图</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. 核心接口规范</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, <code>IConnectionContext</code> API 契约</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. 可观测性与日志</a></td>
<td style="padding: 12px 16px; color: #475569;">三通道有界环形缓冲区（<code>DropOldest</code>）与 60 FPS 流畅 UI 保证</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. 实现与目录结构</a></td>
<td style="padding: 12px 16px; color: #475569;">代码仓库目录体系、命名空间划分及 NuGet 打包发布结构</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. 工业级校验和指南</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Allocation CRC-16(Modbus/CCITT), LRC, XOR BCC 查表算法</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. 高可靠通信路线图</a></td>
<td style="padding: 12px 16px; color: #475569;">13 大工业通信协议图谱、确定性等级与官方集成边界</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/zh-cn/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. 开源许可证指南</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 保证，宽松许可证矩阵（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>
