<!-- Kable Master Documentation Index (Japanese) -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #2563eb 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📑 マスタードキュメントインデックス (MASTER DOCUMENTATION INDEX)</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable マスターインデックス (Master Index)
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Kable リアクティブ通信エンジンの全技術仕様書インデックスおよびナビゲーションマップです。
</p>
</div>

<!-- Sitemap Table -->
<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">分類</th>
<th style="padding: 12px 16px; font-weight: 700;">ドキュメント名</th>
<th style="padding: 12px 16px; font-weight: 700;">主なトピック</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">はじめに (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/README" style="color: #2563eb; text-decoration: none;">概要とクイックスタート (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">NuGet パッケージ、KableClientBuilder、KableSimple、DI コンテナ連携</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #2563eb;">はじめに (Getting Started)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/PROJECT_SPEC" style="color: #2563eb; text-decoration: none;">プロジェクト仕様書 (Project Spec)</a></td>
<td style="padding: 12px 16px; color: #475569;">信頼できる唯一の情報源 (SSOT)、4 大確定アーキテクチャ方針と不変条件</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">アーキテクチャ (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/01_ARCHITECTURE_OVERVIEW" style="color: #059669; text-decoration: none;">01. アーキテクチャ概要 (Overview)</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines + RSocket 3 層トポロジと統合クラス図</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">アーキテクチャ (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/02_CORE_INTERFACES" style="color: #059669; text-decoration: none;">02. コアインターフェース仕様</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IConnectionContext</code>, <code>IProtocolCodec</code>, <code>IDeviceSession</code> 契約</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #059669;">アーキテクチャ (Architecture)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/04_IMPLEMENTATION_LAYOUT" style="color: #059669; text-decoration: none;">04. 実装レイアウトと構成</a></td>
<td style="padding: 12px 16px; color: #475569;">リポジトリツリー、名前空間ルール、NuGet パッケージング構造</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">信頼性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/03_OBSERVABILITY_LOGGING" style="color: #7c3aed; text-decoration: none;">03. オブザーバビリティとログ (Observability)</a></td>
<td style="padding: 12px 16px; color: #475569;">3 チャネルリングバッファ（<code>DropOldest</code>）と 60 FPS UI レンダリング保証</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">信頼性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/05_INDUSTRIAL_CHECKSUMS" style="color: #7c3aed; text-decoration: none;">05. 産業用チェックサム (Checksums)</a></td>
<td style="padding: 12px 16px; color: #475569;">Modbus CRC-16, CCITT, LRC, XOR BCC 0-GC 検索テーブルアルゴリズム</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #7c3aed;">信頼性 (Reliability)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #7c3aed; text-decoration: none;">06. 産業通信ロードマップ</a></td>
<td style="padding: 12px 16px; color: #475569;">13 種の産業プロトコルの決定性クラス比較とロードマップ</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">ガバナンス (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/CONVENTIONS" style="color: #d97706; text-decoration: none;">コーディング規約 (Conventions)</a></td>
<td style="padding: 12px 16px; color: #475569;">行数制限（300〜500行）、同期ブロッキングの禁止、TDD 要件</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700; color: #d97706;">ガバナンス (Governance)</td>
<td style="padding: 12px 16px; font-weight: 600;"><a href="#/ja/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #d97706; text-decoration: none;">07. オープンソースライセンス</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 保証、寛容型ライセンス（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>