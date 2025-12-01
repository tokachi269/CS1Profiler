# CS1Profiler - Cities: Skylines 1 Performance Analysis MOD

Cities: Skylines 1の包括的なパフォーマンス解析MOD。起動時間、実行時パフォーマンス、メソッドレベルのボトルネックを詳細に分析します。

## 🎯 主な機能

### 🚀 起動時間分析
- **詳細タイムライン記録**: ゲーム起動から初回メニューまでの全プロセスを追跡
- **ボトルネック特定**: `BootStrapper.Boot()`, `PackageManager.Ensure()`等の重い処理を特定
- **MOD影響分析**: 各MODの初期化時間を個別に測定

### ⚡ 実行時パフォーマンス測定
- **メソッドレベル計測**: Harmony patchingによる詳細なメソッド実行時間測定
- **MOD性能監視**: FpsBoosterUpdate等のMODメソッドのスパイク検出
- **フレーム別統計**: フレームドロップの原因となるボトルネックを特定
- **メモリ使用量追跡**: 実行中のメモリ消費パターン分析

### 📊 高度な統計解析
- **Python解析ツール**: pandas/matplotlibによる詳細統計・可視化
- **スパイク検出**: 平均実行時間の2倍以上の処理を自動検出
- **カテゴリ別分析**: Manager/AI/Controller系クラスの分類統計
- **呼び出し回数分析**: フレーム間での処理頻度変動を検出

## 🚀 クイックスタート

### 1. インストール
```powershell
# MODのビルドとインストール
.\build\build.ps1
```

### 2. 基本的な使用方法
1. Cities: SkylinesでCS1Profilerを有効化
2. ゲーム内でキーボードショートカットで制御
3. 自動生成されるCSVファイルで詳細分析

### 3. Python解析ツールの使用
```powershell
# 依存関係インストール
cd tools
pip install -r requirements.txt

# CSVファイル解析（自動）
python cs1_profiler_analyzer.py "CS1Profiler_20250825_214742.csv"

# または簡単実行
analyze.bat
```

## 🎮 ゲーム内操作

### キーボードショートカット
- **P**: パフォーマンス測定ON/OFF
- **L**: UI表示切り替え
- **R**: リアルタイム統計表示
- **C**: CSV出力（手動）

### UI パネル
- **パフォーマンスメトリクス**: FPS、メモリ使用量、測定中のメソッド数
- **ホットスポット表示**: 現在最も負荷の高いメソッド上位5位
- **統計サマリー**: 測定開始からの累計統計

## 📊 解析結果と最適化

### 自動生成ファイル
```
# 実行時パフォーマンスデータ
CS1Profiler_YYYYMMDD_HHMMSS.csv

# Python解析結果
analysis_output/
├── method_statistics.csv      # メソッド別詳細統計
├── frame_statistics.csv       # フレーム別統計
├── performance_issues.csv     # 検出された問題
├── analysis_report.txt        # 総合レポート
├── top15_methods.png          # 高負荷メソッドグラフ
├── category_impact.png        # カテゴリ別影響度
├── frame_timeline.png         # フレーム別負荷推移
└── spike_analysis.png         # スパイク分析
```

### よくある最適化パターン

#### 🚨 高負荷メソッドが検出された場合
- **SimulationManager系**: ゲーム速度設定を下げる、都市規模を調整
- **MODメソッド（FpsBooster等）**: 該当MODの設定見直し、無効化検討
- **AI関係**: 人口・交通量の調整

#### 📈 スパイクが多発している場合
- **ガベージコレクション**: メモリ消費の多いアセットを削減
- **ディスクI/O**: SSD使用、アセットファイルの最適化
- **MOD競合**: 類似機能のMODを統合

#### 🔄 呼び出し回数が異常な場合
- **無限ループ**: 該当MODのバグ可能性、無効化して確認
- **イベント重複**: MOD間の競合、設定の見直し

## 🔧 技術的詳細

### Harmonyパッチシステム
- **安全なパッチング**: ジェネリック型、Harmony関連メソッドを自動除外
- **ブラックリスト方式**: 問題のあるMOD（LineToolMod、CSShared等）を自動回避
- **パフォーマンス重視**: Manager/AI/Controller系クラスに特化したパッチ適用

### 測定精度
- **高精度タイマー**: `System.Diagnostics.Stopwatch`による正確な時間測定
- **メモリ追跡**: GC前後のメモリ使用量を正確に記録
- **フレーム同期**: Unity Updateサイクルと同期した測定

### 出力形式
```csv
DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Description
2025-08-25 21:47:42.123,1500,Manager,Method,2.345,1,1024.5,SimulationManager.SimulationStepImpl
```

## 🚨 トラブルシューティング

### ゲームがクラッシュする場合
1. **セーフモード**: Harmonyパッチを無効化して起動
2. **MOD競合**: 他のHarmony MODとの競合確認
3. **ログ確認**: `output_log.txt`でエラー詳細を確認

### データが記録されない場合
1. **権限確認**: ファイル書き込み権限をチェック
2. **パス設定**: 出力先ディレクトリの存在確認
3. **MOD有効化**: Content Managerでの有効化状況確認

### Python解析でエラーが出る場合
```powershell
# 依存関係の再インストール
pip uninstall pandas matplotlib seaborn numpy
pip install -r requirements.txt

# 文字エンコーディング問題
# CSVファイルをUTF-8で再保存
```

## 📈 パフォーマンス向上のベストプラクティス

### 1. 測定設定の最適化
- **短時間測定**: 1-2分の集中的な測定でボトルネック特定
- **特定場面**: 交通渋滞、災害発生時等の負荷場面で測定
- **ベースライン**: MOD無効状態での測定も実施

### 2. データ解釈
- **相対的評価**: 絶対値より他のメソッドとの比較を重視
- **継続監視**: 定期的な測定でパフォーマンス劣化を早期発見
- **カテゴリ分析**: MOD vs ゲーム本体の負荷割合を把握

## 🤝 コントリビューション

- **Issue報告**: バグ、改善要望はGitHub Issuesへ
- **Pull Request**: 機能追加、修正は大歓迎
- **測定データ共有**: 興味深い解析結果があれば共有ください

## 📄 ライセンス

MIT License - 自由に使用、改変、配布可能

---
**CS1Profiler** - Cities: Skylines 1のパフォーマンスを科学的に分析・最適化するためのツール
