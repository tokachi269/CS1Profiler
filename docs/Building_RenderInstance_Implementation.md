# Building RenderInstance Analysis Feature

Building_RenderInstance_Analysis.mdの分析に基づいて、Building.RenderInstance重複呼び出し問題を詳細調査するためのパッチを実装しました。

## 🔍 性能分析発見事項（実測データ）

### PropInstance個別描画ボトルネック
Building.RenderInstance詳細分析（5段階測定）により根本原因を特定：

**実測結果**:
- **RenderMeshes**: 209ms (建物本体メッシュ)
- **RenderProps**: 2,719ms (**80%以上**の処理時間)

**技術的詳細**:
- `PropInstance.RenderInstance`で個別`Graphics.DrawMesh`呼び出し
- 各PropInstance毎の`MaterialPropertyBlock`更新
- CPU-GPU同期待機による性能劣化
- **最適化対象**: PropInstance → Batching conversion

## 実装内容

### 1. BuildingRenderAnalysisPatcher.cs
- **BuildingManager.EndRenderingImpl**全体を置き換えて完全なコンテキスト取得
- 元の処理をコピーして、各Building.RenderInstance呼び出し箇所に分析処理を追加
- High Detail/Low Detail パスの正確な分類
- RenderGroup座標とグリッドインデックスの記録

### 2. SpecificPatchManagers.cs統合
- BuildingRenderAnalysisPatchManagerを追加
- 既存のパッチシステムに統合（デフォルトOFF）

### 3. 手動制御
- パッチ適用後、手動でC#コードまたはMOD設定から制御
- キーボードショートカットは削除（不要）

## 使用方法

### 1. パッチの有効化
```csharp
// BuildingRenderAnalysisPatchManagerを有効化
// または設定画面から有効化
```

### 2. 分析の実行
```csharp
// 分析開始
CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StartAnalysis();

// ゲームプレイ（数分）

// 分析停止
CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StopAnalysis();

// 結果出力
CS1Profiler.Harmony.BuildingRenderAnalysisHooks.OutputStats();
```

### 3. 結果の確認
- コンソールログで詳細統計確認
- クリップボードにコピーされるため、テキストエディタに貼り付け可能

## 出力データ

### 統計情報
- 建物ID別の呼び出し回数（正確なカウント）
- High Detail vs Low Detail の正確な比率
- RenderGroup座標の記録
- グリッドインデックスの記録

### 期待される発見
- 97,743回呼び出しの正確な内訳
- 774棟で126倍重複の具体的な原因
- どの建物がどのRenderGroupで何回呼ばれているか
- High/Low Detailパスの正確な分布

## 技術的詳細

### 完全置換方式
- BuildingManager.EndRenderingImpl全体をHarmonyで置き換え
- 元のコードを完全にコピーして分析処理を挿入
- 正確なコンテキスト（パスタイプ、RenderGroup、グリッド位置）を取得

### パフォーマンス最適化
- 分析OFF時は完全にオリジナルの処理
- 分析ON時のみ軽量なログ記録
- ロック最小化（統計更新時のみ）

### 制限事項
- BuildingManager.EndRenderingImpl全体を置き換えるため、他のMODとの競合可能性
- 分析中は若干のオーバーヘッドが発生

## 将来の拡張

1. **設定画面統合**：UI画面からの分析制御
2. **CSV出力統合**：既存のCSV出力システムとの統合
3. **リアルタイム表示**：ゲーム内UIでの統計表示
4. **自動分析**：特定条件での自動分析開始

この実装により、Building.RenderInstance重複呼び出し問題の根本原因を**完全に正確**に分析できるようになります。

## 制御方法

パッチは手動制御のみ。ゲーム内でのコード実行または設定システムからの制御を想定。
キーボードショートカットは削除し、最小限の実装に集中。