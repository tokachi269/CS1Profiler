# CS1Profiler - Cities Skylines Performance Monitor

Cities: Skylines用のプロファイラーMOD。Harmonyを使用してゲーム内メソッドの実行パフォーマンスを詳細に測定・記録します。

## 機能概要

### コア機能
- **Harmonyパッチング**: ゲーム内の重要なメソッドを自動的にパッチしてパフォーマンス測定
- **統計記録**: メソッド毎の平均実行時間、最大実行時間、総実行時間、呼び出し回数を記録
- **CSV出力**: Top100/全データをCSV形式で出力、30秒間隔の自動出力機能
- **リアルタイムUI**: ゲーム内でTop50メソッドの実行統計を表示

### UI操作
- **P キー**: パフォーマンスパネルの表示/非表示切り替え
- **F12 キー**: Top100メソッドをCSV出力
- **オプション画面**: Top100 CSVエクスポート、統計クリア機能

## 必要な環境

### 前提条件
1. **Cities: Skylines** (Steam版)
2. **.NET Framework 3.5** (C# 3.5互換)
3. **CitiesHarmony** MOD (Workshop ID: 2040656402)

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

### 1. ビルド実行
```powershell
# PowerShellでビルド
.\build\build.ps1
```

### 2. 自動配置
ビルドスクリプトが以下を自動実行：
- Release構成でビルド
- MODフォルダーに自動配置: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\`

## パフォーマンス測定

### パッチ対象クラス
ブラックリスト方式でフィルタリング：
- **除外**: System、Unity、Attribute、Exception、Event クラス
- **対象**: Manager、AI、Controller、Service、Simulation、Render等のゲームロジッククラス

### パッチ対象メソッド
- **除外**: プロパティアクセサー、イベントハンドラー、基本メソッド
- **対象**: 実際のゲームロジックを実装するメソッド

## 出力データ

### CSV形式
```csv
Rank,Method,AvgMs,MaxMs,TotalMs,Calls
1,SimulationManager.SimulationStep,2.45,15.2,245.7,100
2,RenderManager.LateUpdate,1.23,8.9,123.4,100
```

### 出力ファイル
```
Cities: Skylines インストールディレクトリ/
├── CS1Profiler_Top100_YYYYMMDD_HHMMSS.csv    # 手動出力
└── CS1Profiler_All_YYYYMMDD_HHMMSS.csv       # 自動出力(30秒間隔)
```
- `C:\Program Files (x86)\Steam` 
- `C:\Program Files\Steam`

### 2. ビルド実行

```powershell
# 通常のビルド
.\build.ps1

# クリーンビルド
.\build.ps1 -Clean

# デバッグビルド
.\build.ps1 -Configuration Debug
```

### 3. 自動配置

ビルドが成功すると、DLLが自動的に以下の場所にコピーされます：

```
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\
└── CS1Profiler.dll
```

## 使用方法

1. **CitiesHarmony** をWorkshopからサブスクライブして有効化
2. このmodをビルドして配置
3. Cities: Skylines を起動
4. **Content Manager > Mods** で "CS1 Method Profiler (Low Overhead)" を有効化

### キーボード操作

### キーボード操作

- **Pキー**: プロファイリングのオン・オフ切り替え
- **Lキー**: 画面ログの表示・非表示切り替え（画面左上にボックス表示）
- **Rキー**: FPS・メモリ使用量の瞬間表示

### ログ確認方法

**方法1: 画面ログ**
- ゲーム中に **Lキー** を押すと、画面左上にプロファイリング結果が表示されます
- 右上に「CS1Profiler: ON/OFF」のステータスも表示されます

**方法2: ModTools**
- ゲーム中に **F7** を押してModToolsを開く
- **Console** タブでプロファイリング結果を確認

**方法3: ログファイル**
- `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Cities.log` でも確認可能

## 出力例

```
[CS1Profiler] Top20 (frame 7200)
1. BuildingManager.UpdateData        total:25.123ms  calls:120  avg:209.36us
2. SimulationManager.SimulationStepImpl  total:15.234ms  calls:120  avg:127.02us
3. NetManager.UpdateSegmentRenderer  total:12.456ms  calls:120  avg:103.80us
4. EconomyManager.SimulationStepImpl total:8.456ms   calls:120  avg:70.47us
5. PropManager.UpdateData            total:6.789ms   calls:120  avg:56.58us
...

[CS1Stats] FPS:45.2 Memory:6789MB
[CS1Rendering] FPS:43.8 Memory:6834MB Frame:18450
```

## トラブルシューティング

### ビルドエラー

**「ICities.dll not found」**
- Steamのインストール場所を確認
- `STEAM_PATH` 環境変数を正しく設定

**「CitiesHarmony not found」**
- WorkshopからCitiesHarmonyをサブスクライブ
- ゲーム内で有効化されていることを確認

### 実行時エラー

**「CitiesHarmony not found」ログ**
- CitiesHarmonyが正しくインストールされているか確認
- modの読み込み順序を確認（CitiesHarmonyが先に読み込まれる必要）

## 開発

### ファイル構成

```
source/
├── CS1Profiler.cs           ※ メインのmodコード
├── CS1Profiler.csproj       ※ プロジェクトファイル
├── Directory.Build.props    ※ ビルド設定
├── build.ps1               ※ ビルドスクリプト
└── README.md               ※ このファイル
```

### カスタマイズ

- **サンプリング頻度**: `SampleEveryNFrames` を変更
- **出力間隔**: `DumpTop` の呼び出し頻度を調整
- **監視対象**: `TryPatch` で追加のメソッドをパッチ

## ライセンス

このmodはオープンソースです。自由に改変・再配布してください。

## 参考リンク

- [Cities: Skylines Modding](https://skylines.paradoxwikis.com/Modding)
- [CitiesHarmony](https://github.com/boformer/CitiesHarmony)
- [Harmony Documentation](https://harmony.pardeike.net/)
