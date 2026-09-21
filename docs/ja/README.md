<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #1d4ed8 100%); border-radius: 14px; padding: 36px 30px; margin-bottom: 30px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>🔌 産業用高信頼性ハードウェア通信エンジン</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 30px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      Kable 技術ドキュメント (日本語)
</h1>
<p style="margin: 0; font-size: 15px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Microsoft Bedrock の <code>System.IO.Pipelines</code> トランスポート抽象化と RSocket リアクティブインタラクションパターンを融合した、超高性能・Zero-Allocation ハードウェア通信フレームワークです。
</p>
<div style="margin-top: 20px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #2563eb; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">.NET 10.0 / 8.0 / netstandard2.0</span>
<span style="background: #059669; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">0-GC パイプライン I/O</span>
<span style="background: #7c3aed; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">Fail-Fast 安全状態遷移</span>
<span style="background: #d97706; color: #ffffff; font-size: 11.5px; font-weight: 600; padding: 4px 12px; border-radius: 12px;">3チャネル可観測性リングバッファ</span>
</div>
</div>

<!-- Feature Grid Cards -->
<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 32px;">
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #2563eb;">
<div style="font-weight: 700; color: #1d4ed8; font-size: 15px; margin-bottom: 8px;">⚡ Bedrock Pipelines ゼロコピー</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        ソケットおよびシリアル通信のメモリコピーを完全排除し、<code>ReadOnlySequence&lt;byte&gt;</code> ベクトル加速フレーミングによりリクエストあたり 1KB 未満のメモリ消費を実現します。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #059669;">
<div style="font-weight: 700; color: #047857; font-size: 15px; margin-bottom: 8px;">🔀 ハイブリッドトランザクションルーター</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        識別IDのないレガシーASCII機器は非同期FIFOロックで安全に直列化し、最新プロトコルではマイクロ秒レイテンシの Lock-Free 多重化処理を提供します。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #dc2626;">
<div style="font-weight: 700; color: #b91c1c; font-size: 15px; margin-bottom: 8px;">🛡️ Fail-Fast 安全ポリシー</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        物理リンク切断時に無駄な再試行を行わず、すべての待機スレッドへ <code>DeviceDisconnectedException</code> を即座に送出し、設備を即座に安全状態へ移行させます。
</div>
</div>
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.03); border-top: 3px solid #7c3aed;">
<div style="font-weight: 700; color: #6d28d9; font-size: 15px; margin-bottom: 8px;">📊 3チャネル可観測性リングバッファ</div>
<div style="font-size: 13px; color: #475569; line-height: 1.5;">
        周期的テレメトリ、コンソール通信、自発的アラームを独立した有限リングバッファ（<code>DropOldest</code>）で物理分離し、100Hz 高速通信下でも UI フリーズを防止します。
</div>
</div>
</div>

<!-- Quick Start Header -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    🚀 開発者クイックスタート (Developer Quick Start)
</h2>

<!-- Step 1: NuGet -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      ステップ 1. パッケージのインストール (NuGet)
</div>
<div style="padding: 14px 18px;">

```bash
# コア通信エンジン (TCP, Serial, NamedPipe, コーデック, クライアントビルダー)
dotnet add package Kable

# 純粋な抽象化インターフェース専用
dotnet add package Kable.Core
```

</div>
</div>

<!-- Step 2: Fluent Builder -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 18px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 10px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      ステップ 2. Fluent Builder によるセッション構築 (`KableClientBuilder`)
</div>
<div style="padding: 14px 18px;">
<p style="font-size: 13px; color: #64748b; margin: 0 0 10px 0;">
        わずか 3 行のコードでトランスポート層とプロトコルコーデックをクリーンに定義：
</p>

```csharp
using Kable.Extensions;
using Kable.Codecs;
using Kable.Exceptions;

// 1. セッションインスタンスのビルド
await using var session = new KableClientBuilder<string>()
    .UseTcp("192.168.0.100", 9000)
    // .UseSerialPort("COM3", baudRate: 115200)
    // .UseNamedPipe("efem_ipc_pipe")
    .UseCodec(new AsciiLineCodec(delimiter: 0x0A)) // LF 区切り
    .Build();

// 2. 非同期 I/O パイプラインの開始
await session.StartAsync();

// 3. 3秒ウォッチドッグ制限付きリクエスト-レスポンス RPC
string response = await session.RequestAsync<string>("READ:TEMP", TimeSpan.FromSeconds(3));
Console.WriteLine($"温度レスポンス: {response}");

// 4. リアルタイム非同期テレメトリストリームの受信
await foreach (var packet in session.Stream)
{
    Console.WriteLine($"テレメトリパケット: {packet}");
}
```

</div>
</div>

<!-- Document Links Table -->
<h2 style="font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 16px 0; display: flex; align-items: center; gap: 8px;">
<span style="background: #2563eb; width: 6px; height: 22px; border-radius: 3px; display: inline-block;"></span>
    📑 技術仕様書サイトマップ
</h2>

<div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px;">
<table style="width: 100%; border-collapse: collapse; text-align: left; font-size: 13.5px;">
<thead>
<tr style="background: #f1f5f9; color: #334155; border-bottom: 2px solid #cbd5e1;">
<th style="padding: 12px 16px; font-weight: 700;">仕様書</th>
<th style="padding: 12px 16px; font-weight: 700;">主要スコープと仕様</th>
</tr>
</thead>
<tbody>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/INDEX" style="color: #2563eb; text-decoration: none;">INDEX.md</a></td>
<td style="padding: 12px 16px; color: #475569;">マスタードキュメントインデックスと 4 大確定アーキテクチャ設計方針</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/01_ARCHITECTURE_OVERVIEW" style="color: #2563eb; text-decoration: none;">01. アーキテクチャ概要</a></td>
<td style="padding: 12px 16px; color: #475569;">Bedrock Pipelines トランスポート層、RSocket モデルおよびクラス図</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/02_CORE_INTERFACES" style="color: #2563eb; text-decoration: none;">02. コアインターフェース仕様</a></td>
<td style="padding: 12px 16px; color: #475569;"><code>IDeviceSession</code>, <code>IProtocolCodec</code>, <code>IConnectionContext</code> 規約</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/03_OBSERVABILITY_LOGGING" style="color: #2563eb; text-decoration: none;">03. オブザーバビリティとロギング</a></td>
<td style="padding: 12px 16px; color: #475569;">3チャネルリングバッファ（<code>DropOldest</code>）と 60 FPS UI レンダリング保証</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/04_IMPLEMENTATION_LAYOUT" style="color: #2563eb; text-decoration: none;">04. 実装レイアウトと構成</a></td>
<td style="padding: 12px 16px; color: #475569;">リポジトリ構成階層、名前空間ルール、NuGet パッケージング構造</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/05_INDUSTRIAL_CHECKSUMS" style="color: #2563eb; text-decoration: none;">05. 産業用チェックサムガイド</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Allocation CRC-16 (Modbus/CCITT), LRC, XOR BCC 検索テーブルアルゴリズム</td>
</tr>
<tr style="border-bottom: 1px solid #e2e8f0; background: #ffffff;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP" style="color: #2563eb; text-decoration: none;">06. 高信頼性通信ロードマップ</a></td>
<td style="padding: 12px 16px; color: #475569;">産業プロトコル 13 種の決定性クラス比較と統合境界定義</td>
</tr>
<tr style="background: #f8fafc;">
<td style="padding: 12px 16px; font-weight: 700;"><a href="#/ja/07_OPENSOURCE_LICENSING_AND_COMPLIANCE" style="color: #2563eb; text-decoration: none;">07. オープンソースライセンス</a></td>
<td style="padding: 12px 16px; color: #475569;">Zero-Copyleft 保証、寛容型ライセンスマトリックス（Apache-2.0, MIT, BSD）</td>
</tr>
</tbody>
</table>
</div>

</div>
