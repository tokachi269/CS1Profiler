# CS1 Startup Performance Analyzer

Cities: Skylines 1の起動時の性能問題を詳細に分析し、ボトルネックを特定するためのMODです。

## 🎯 主な機能

### 起動解析
- **詳細なタイムライン記録**: ゲーム起動から初回メニューまでの全プロセスを追跡
- **ボトルネック特定**: 最も時間がかかる処理を自動的に特定
- **メモリ使用量監視**: 起動中のメモリ使用量変化を詳細に記録
- **MOD影響分析**: 各MODの初期化時間を個別に測定

### 重要な測定ポイント
- `BootStrapper.Boot()`: ゲーム核心エンジンの初期化時間
- `PackageManager.Ensure()`: MOD/アセット読み込み時間（最大のボトルネック）
- `LoadingExtension`: 個別MODの初期化時間
- メモリ使用量の変化パターン

## 🚀 クイックスタート

### 1. インストール
```bash
dotnet build --configuration Release
install_and_test.bat
```

### 2. 使用方法
1. Cities: Skylines を起動
2. Content Manager で `CS1 Startup Performance Analyzer` を有効化
3. ゲームを再起動（起動時間の測定が開始されます）
4. メインメニューが表示されたら測定完了

### 3. 結果の確認
```bash
# 生成されたCSVファイルを自動分析
python analyze_startup.py
```

## 📊 起動高速化のヒント

### 1. PackageManager.Ensure() が遅い場合
- **不要なアセットを削除**: Steam Workshop で不要なアイテムを購読解除
- **SSDの使用**: HDDからSSDへの移行で大幅改善

### 2. 特定MODが遅い場合
- **MODの見直し**: 初期化時間が長いMODを特定し、必要性を再評価

### 3. メモリ使用量が多い場合
- **アセット数の削減**: 高解像度テクスチャのアセットを減らす

## 🎮 実行時操作

### キーボードショートカット（ゲーム中）
- **P**: プロファイリングON/OFF切り替え
- **L**: ログ表示ON/OFF切り替え
- **R**: 現在の統計表示（FPS、メモリ使用量）

### 出力ファイル
```
Documents\My Games\Cities_Skylines\CS1Profiler_startup_YYYYMMDD_HHMMSS.csv
```
