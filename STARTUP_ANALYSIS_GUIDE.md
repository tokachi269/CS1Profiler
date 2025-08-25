# CS1Profiler Performance Analysis Guide

## 概要
CS1Profilerは、Cities: Skylines 1のメソッド実行パフォーマンスを詳細に分析するMODです。Harmonyパッチングを使用してゲーム内の重要なメソッドを監視し、実行時間やコール数を記録・分析します。

## 主な機能

### 1. リアルタイムパフォーマンス監視
- ゲーム実行中のメソッド実行時間を測定
- メソッド毎の統計情報を蓄積（平均時間、最大時間、総時間、呼び出し回数）
- Top50メソッドをゲーム内UIで表示

### 2. 包括的なメソッドパッチング
- **対象クラス**: Manager、AI、Controller、Service、Simulation、Renderクラス等
- **フィルタリング**: ブラックリスト方式でシステムクラスを除外
- **メソッド選択**: プロパティやイベントハンドラーを除く実際のゲームロジック

### 3. CSV出力機能
- **手動出力**: F12キーでTop100メソッドをCSV出力
- **自動出力**: 30秒間隔で全データをCSV出力
- **統一フォーマット**: Rank,Method,AvgMs,MaxMs,TotalMs,Calls

## インストール・セットアップ

### 1. ビルド
```powershell
.\build\build.ps1
```

### 2. 自動配置
ビルドスクリプトが自動的に以下のフォルダに配置：
```
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\
└── CS1Profiler.dll
```

### 3. 必要なMOD
- **CitiesHarmony** (Workshop ID: 2040656402) が必要

## 使用方法

### 基本操作
1. Cities: Skylinesを起動
2. Content ManagerでCS1Profilerを有効化
3. ゲームを開始すると自動的にプロファイリング開始

### キーボードショートカット
- **P キー**: パフォーマンスパネルの表示/非表示
- **F12 キー**: Top100メソッドをCSV出力

### UI操作
- **パフォーマンスパネル**: Top50メソッドの実行統計を表示
- **Refreshボタン**: 表示データを最新に更新
- **Clearボタン**: 収集した統計をリセット
- **オプション画面**: Export Top100 CSV、Clear Stats機能

## データ分析

### CSV出力ファイル
```
Cities: Skylines インストールディレクトリ/
├── CS1Profiler_Top100_YYYYMMDD_HHMMSS.csv    # 手動出力
└── CS1Profiler_All_YYYYMMDD_HHMMSS.csv       # 30秒間隔自動出力
```

### データフォーマット
```csv
Rank,Method,AvgMs,MaxMs,TotalMs,Calls
1,SimulationManager.SimulationStep,2.45,15.2,245.7,100
2,RenderManager.LateUpdate,1.23,8.9,123.4,100
```

### パフォーマンス分析のポイント

#### 1. 重いメソッドの特定
- **AvgMs**: 平均実行時間が高いメソッドを特定
- **MaxMs**: スパイクが発生しているメソッドを確認
- **TotalMs**: 累積時間が大きいメソッドを調査

#### 2. 頻繁に呼ばれるメソッド
- **Calls**: 呼び出し回数が多いメソッドの最適化検討
- フレーム毎に呼ばれるUpdate系メソッドに注目

#### 3. パフォーマンス最適化の優先順位
```
優先度1: AvgMs > 5.0 かつ Calls > 100 のメソッド
優先度2: TotalMs がTop10に入るメソッド
優先度3: MaxMs が異常に高いメソッド（スパイク対策）
```

## トラブルシューティング

### よくある問題

#### 1. パフォーマンスパネルが表示されない
**原因**: Pキーが他のMODと競合
**解決**: 他のMODを一時無効化して確認

#### 2. CSVファイルが出力されない
**原因**: ファイル書き込み権限の問題
**解決**: Cities: Skylinesを管理者権限で実行

#### 3. ゲームが重くなる
**原因**: 大量のメソッドがパッチされている
**解決**: 一時的にMODを無効化してパフォーマンス影響を確認

### ログ確認
MODの動作状況は以下のログファイルで確認：
```
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\output_log.txt
```
2. メモリ使用量をCSVで確認
3. 32bit制限（約3GB）に近づいていないか確認

### 問題2: PackageManager.Ensure() が非常に遅い
**原因**: 大量のアセット/MODまたは破損したファイル
**解決**:
1. 不要なアセットを削除
2. Steam Workshop のキャッシュをクリア
3. アセットの整合性チェック実行

### 問題3: 特定MODのOnCreatedが遅い
**原因**: そのMODの初期化処理に問題
**解決**:
1. MODを一時的に無効化して確認
2. MOD作者に報告
3. 代替MODを検討

## 高度な分析

### 複数回の測定による平均化
```powershell
# 複数回起動してデータを収集
for ($i=1; $i -le 5; $i++) {
    # Cities Skylines起動・終了を繰り返し
    # CSVデータを収集・分析
}
```

### カスタム分析スクリプト
CSVデータをExcelやPythonで分析し、詳細なボトルネック特定が可能です。

## 貢献・改善
このプロファイラーは継続的に改善されています。問題や改善提案があれば、以下にご報告ください：

- GitHub Issues
- Modding コミュニティフォーラム
- Steam Workshop コメント

## ライセンス
MIT License - 自由に改変・配布可能

---

**重要**: このMODは診断・分析専用です。ゲームプレイには最小限の影響しかありませんが、正確な測定のためには他のMODとの相互作用を考慮してください。
