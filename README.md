# CS1Profiler

Cities: Skylines 1のメソッド実行パフォーマンスを詳細にプロファイリングするMODです。
Harmonyを使用してゲーム内のメソッドをパッチし、実行時間とコール数を記録・分析します。

## 🎯 主な機能

### パフォーマンスプロファイリング
- **Harmonyパッチング**: Manager、AI、Controller、Service、Simulation、Renderクラス等のメソッドを自動的にパッチ
- **実行統計**: メソッド毎の平均実行時間、最大実行時間、総実行時間、呼び出し回数を記録
- **リアルタイム監視**: ゲーム実行中のメソッド実行パフォーマンスを監視

### CSV出力機能
- **TopNエクスポート**: 最も重い上位メソッドをCSV形式で出力
- **全データエクスポート**: 記録されたすべてのメソッド統計をCSV出力
- **自動出力**: 30秒間隔で全データを自動的にCSV出力

### UI コンポーネント
- **パフォーマンスパネル**: ゲーム内でTop50メソッドをIMGUIで表示
- **オプション画面**: MOD設定画面でTop100 CSVエクスポートと統計クリア機能
- **キーボード操作**: P キーでパネル切り替え、F12 キーでCSV出力

## 🚀 クイックスタート

### 1. ビルド
```powershell
# PowerShellで実行
.\build\build.ps1
```

### 2. 使用方法
1. Cities: Skylines を起動
2. Content Manager で `CS1Profiler` を有効化
3. ゲームを開始すると自動的にプロファイリング開始

### 3. 操作方法
- **P キー**: パフォーマンスパネルの表示/非表示
- **F12 キー**: Top100 メソッドをCSV出力
- **オプション > CS1Profiler**: Top100 CSV出力、統計クリア

## 📊 出力データ形式

### CSV ファイル形式
```csv
Rank,Method,AvgMs,MaxMs,TotalMs,Calls
1,SimulationManager.SimulationStep,2.45,15.2,245.7,100
2,RenderManager.LateUpdate,1.23,8.9,123.4,100
...
```

### ファイル出力先
```
Cities: Skylines インストールディレクトリ/
├── CS1Profiler_Top100_YYYYMMDD_HHMMSS.csv    (手動/F12キー出力)
└── CS1Profiler_All_YYYYMMDD_HHMMSS.csv       (30秒間隔自動出力)
```

## 🎮 実行時操作

### キーボードショートカット
- **P**: パフォーマンスパネル表示切り替え
- **F12**: Top100 メソッドCSV出力

### パフォーマンスパネル機能
- Top50メソッドの実行統計表示
- Refresh ボタンでデータ更新
- Clear ボタンで統計リセット

### オプション画面機能
- Export Top100 CSV: 上位100メソッドをCSV出力
- Clear Stats: 収集した統計データをクリア

## 📋 技術仕様

### パッチ対象クラス
ブラックリスト方式でフィルタリング（システムクラスを除く全ゲームロジッククラス）
- 除外: Attribute、Exception、Event、System、Unity フレームワーククラス
- 対象: Manager、AI、Controller、Service、Simulation、Render等のゲームロジッククラス

### パッチ対象メソッド
ブラックリスト方式でフィルタリング
- 除外: プロパティ、イベント、基本メソッド（ToString、GetHashCode等）
- 対象: 実際のゲームロジックを実装するメソッド

## 🔧 開発・デバッグ

### ログ出力
MODの動作状況は Cities: Skylines のログファイルに出力されます：
```
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\output_log.txt
```
