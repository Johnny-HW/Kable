<!-- Kable Master Documentation Index (Simplified Chinese) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 技术文档总索引 (MASTER DOCUMENTATION INDEX)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 主索引目录 (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable 响应式硬件通信引擎的技术规范全集索引与导航地图。
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">分类</th>
<th style="padding: 12px 16px; font-weight: 700;">文档名称</th>
<th style="padding: 12px 16px; font-weight: 700;">主要涵盖内容</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">快速入门 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/README" style="color: #2563eb; text-decoration: none;">概览与快速上手 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet 软件包、KableClientBuilder、KableSimple 外观、DI 容器集成</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">快速入门 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">项目技术规范书 (Project Spec)</a></td>
<td style="padding: 12px 16px; color: #475569;">单一真实来源 (SSOT)、4 大核心架构决策与不变性约束</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架构设计 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. 架构总览 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3 层拓扑与统一类图</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架构设计 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. 核心接口规范书</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> 契约</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架构设计 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. 实现与目录结构</a></td>
<td style="padding: 12px 16px; color: #475569;">代码仓库目录树、命名空间准则与 NuGet 打包布局</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. 可观测性与日志 (Observability)</a></td>
<td style="padding: 12px 16px; color: #475569;">三通道有界环形缓冲区（<code>DropOldest</code>）与 60 FPS 流畅 UI 保证</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. 工业级校验和 (Checksums)</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, XOR BCC 0-GC 查表算法</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. 工业通信路线图</a></td>
<td style="padding: 12px 16px; color: #475569;">13 大工业通信协议图谱、确定性等级与演进路线</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">治理规范 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/CONVENTIONS" style="color: #d97706; text-decoration: none;">编码规范 (Conventions)</a></td>
<td style="padding: 12px 16px; color: #475569;">代码行数限制（300~500）、严禁同步阻塞及 TDD 测试要求</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">治理规范 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-cn/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. 开源许可证与合规指南</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 承诺、宽松许可证矩阵（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>