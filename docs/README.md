# CS1Profiler - Cities Skylines Performance Monitor

Cities: Skylines用の高性能プロファイラーMOD。Harmonyパッチングによる詳細なパフォーマンス測定と、ゲーム最適化機能を提供します。

## 最新機能 (v2.0)

### 新機能追加
- **GameSettings最適化**: 2.6MB保存ファイルの書き込み間隔を1秒→1分に最適化
- **PloppableAsphaltFix最適化**: 838ms スパイクを解消し、透明化バグも修正
- **警告ダイアログ**: 重い計測開始時に多言語対応の警告とセーブ推奨
- **Stop機能強化**: CSV自動出力を即座に停止し、安全な終了処理
- **ThreadProfiler除外**: プロファイラー監視プロファイラーによる無限ループを防止

### パフォーマンス改善
- **MODスケーリング**: 150+MOD環境での安定動作とパッチ適用率向上
- **例外処理強化**: 個別パッチ失敗時もシステム全体の継続動作
- **メモリ最適化**: キャッシュ機能とリフレクション最小化
- **UI応答性**: 10フレーム間隔でのUI処理最適化

## 機能概要

### コア機能
- **Harmonyパッチング**: ゲーム内の重要なメソッドを自動的にパッチしてパフォーマンス測定
- **統計記録**: メソッド毎の平均実行時間、最大実行時間、総実行時間、呼び出し回数を記録
- **CSV出力**: Top100/全データをCSV形式で出力、30秒間隔の自動出力機能
- **リアルタイムUI**: ゲーム内でTop50メソッドの実行統計を表示
- **ゲーム最適化**: 重いMODの動作を最適化してフレームレート向上

### UI操作
- **設定画面**: Content Manager > Mods で詳細設定
- **一括制御**: 全ての最適化パッチを一括有効/無効化
- **個別設定**: GameSettings、PloppableAsphaltFix等を個別制御
- **パフォーマンス分析**: 重い処理の自動警告とセーブファイル推奨

## 必要な環境

### 前提条件
1. **Cities: Skylines** (Steam版)
2. **.NET Framework 4.7.2** 以上
3. **Harmony 2.x** (自動インストール)

### 対応MOD
- **PloppableAsphaltFix**: スパイク除去
- **GameSettings**: 数MB保存ファイル最適化
- **ThreadProfiler**: 無限ループ防止による負荷軽減

### 参照DLL
Cities SkylinesのゲームファイルからDLLを参照：
```
C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\
├── ICities.dll          # MOD開発API
├── UnityEngine.dll      # Unityエンジン
├── ColossalManaged.dll  # ゲーム基本機能
└── Assembly-CSharp.dll  # ゲームロジック
```

## ビルド・配置

### 1. 開発環境準備
```powershell
# Gitリポジトリのクローン
git clone https://github.com/tokachi269/CS1Profiler.git
cd CS1Profiler

# PowerShellビルドスクリプト実行
.\build\build.ps1
```

### 2. 自動配置
ビルドスクリプトが以下を自動実行：
- Release構成でビルド
- MODフォルダーに自動配置: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\`
- 依存関係の自動解決

## パフォーマンス測定

### スマートパッチング
ブラックリスト方式でフィルタリング：
- **除外**: System、Unity、Attribute、Exception、Event、ThreadProfiler クラス
- **対象**: Manager、AI、Controller、Service、Simulation、Render等のゲームロジッククラス
- **例外処理**: 個別パッチ失敗時もシステム継続動作
- **MODスケーリング**: 150+MOD環境での安定したパッチ適用

### 測定対象メソッド
- **除外**: プロパティアクセサー、イベントハンドラー、基本メソッド
- **対象**: 実際のゲームロジックを実装するメソッド
- **最適化**: 重いメソッドの自動最適化（GameSettings、PloppableAsphaltFix等）

## 出力データ

### CSV形式
```csv
MethodName,TotalDuration(ms),CallCount,AvgDuration(ms),MaxDuration(ms)
SimulationManager.SimulationStep,245.70,100,2.457,15.2
GameSettings.MonitorSave,1584.6,20,79.23,150.4
PloppableAsphaltFix.OnUpdate,16775.6,20,838.78,1250.0
RenderManager.LateUpdate,123.4,100,1.234,8.9
EconomyManager.SimulationStepImpl,84.56,120,0.705,3.2
```

### 出力ファイル
```
Cities: Skylines インストールディレクトリ/
└── CS1Profiler_YYYYMMDD_HHMMSS.csv    # メイン出力（現在のみ）
```

### 自動出力機能
- **30秒間隔**: 自動CSV出力（設定で変更可能）
- **停止時出力**: Stop押下時の即座CSV生成
- **安全停止**: CSV書き込み完了後のパッチ無効化

## 使用方法

### 1. 初期設定
1. このMODをビルドして配置
2. Cities: Skylinesを起動
3. **Content Manager > Mods** で "CS1 Method Profiler" を有効化
4. **警告ダイアログ**: 計測開始前に重い処理の警告とセーブ推奨

### 2. 基本操作
- **Settings画面**: 全ての機能を設定画面から制御
- **一括制御**: "Enable All Optimizations" で全最適化を有効化
- **個別制御**: GameSettings、PloppableAsphaltFix等を個別に設定
- **Stop機能**: 安全な停止処理（CSV書き込み→パッチ無効化）

## 出力例

### プロファイリング結果（ログ）
```
[CS1Profiler] Performance Analysis Results (frame 7200)
1. GameSettings.MonitorSave          79.23ms  [OPTIMIZED: 1s→1min interval]
2. PloppableAsphaltFix.OnUpdate      838.78ms [OPTIMIZED: Spike eliminated]
3. SimulationManager.SimulationStep   2.45ms  [NORMAL]
4. RenderManager.LateUpdate           1.23ms  [NORMAL]
5. EconomyManager.SimulationStepImpl  0.89ms  [NORMAL]
```

### 最適化ログ
```
[CS1Profiler] GameSettings optimization enabled - Save interval: 1s → 1min
[CS1Profiler] PloppableAsphaltFix transparency bug fixed
[CS1Profiler] ThreadProfiler infinite loop prevention active
[CS1Profiler] Total optimizations applied: 3/3
```

### CSVファイル例
```csv
MethodName,TotalDuration(ms),CallCount,AvgDuration(ms),MaxDuration(ms)
SimulationManager.SimulationStep,245.70,100,2.457,15.2
GameSettings.MonitorSave,1584.6,20,79.23,150.4
PloppableAsphaltFix.OnUpdate,16775.6,20,838.78,1250.0
```