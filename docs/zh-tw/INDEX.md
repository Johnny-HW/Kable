<!-- Kable Master Documentation Index (Traditional Chinese) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 技術規格文件主索引 (MASTER DOCUMENTATION INDEX)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 主索引目錄 (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable 響應式硬體通訊引擎之全體技術規格書目錄與導航地圖。
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">分類</th>
<th style="padding: 12px 16px; font-weight: 700;">文件名稱</th>
<th style="padding: 12px 16px; font-weight: 700;">主要涵蓋內容</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">快速入門 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/README" style="color: #2563eb; text-decoration: none;">概觀與快速上手 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet 套件、KableClientBuilder、KableSimple 外觀、DI 容器整合</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">快速入門 (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">專案規格書 (Project Spec)</a></td>
<td style="padding: 12px 16px; color: #475569;">單一真實來源 (SSOT)、4 大核心架構決策與不變性約束</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架構設計 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. 架構總覽 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3 層拓撲與整合類別圖</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架構設計 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. 核心介面規範書</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> 契約</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">架構設計 (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. 實作與目錄結構</a></td>
<td style="padding: 12px 16px; color: #475569;">儲存庫檔案樹、命名空間準則與 NuGet 封裝配置</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. 可觀測性與記錄 (Observability)</a></td>
<td style="padding: 12px 16px; color: #475569;">三通道有界環狀緩衝區（<code>DropOldest</code>）與 60 FPS 流暢 UI 保證</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. 工業級校驗碼 (Checksums)</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, XOR BCC 0-GC 查表格演算法</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">可靠性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. 工業通訊藍圖</a></td>
<td style="padding: 12px 16px; color: #475569;">13 大工業通訊協定光譜、確定性等級與演進藍圖</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">治理規範 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/CONVENTIONS" style="color: #d97706; text-decoration: none;">程式碼規範 (Conventions)</a></td>
<td style="padding: 12px 16px; color: #475569;">行數限制（300~500）、嚴禁同步阻塞及 TDD 測試要求</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">治理規範 (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/zh-tw/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. 開源授權與合規指引</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 承諾、寬鬆授權矩陣（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>