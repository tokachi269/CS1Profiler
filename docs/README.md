# CS1 Profiler - Cities Skylines Performance Monitor

Cities: Skylines用の軽量プロファイラーmod。主要なシミュレーションメソッドのパフォーマンスを測定します。

## 機能

- `SimulationStepImpl` メソッドの実行時間測定
- UI更新処理のプロファイリング
- 低オーバーヘッド設計
- ModTools (F7) またはログファイルで結果確認

## 必要な環境

### 前提条件

1. **Cities: Skylines** (Steam版)
2. **.NET Framework 4.8** または **.NET 6+ SDK**
3. **CitiesHarmony** mod (Workshop ID: 2040656402)

### 参照DLLの場所

Cities SkylinesのゲームファイルからDLLを参照します：

```
C:\Program Files (x86)\SteamLibrary\steamapps\common\Cities_Skylines\Cities_Data\Managed\
├── ICities.dll          ※ Mod開発の基本API
├── UnityEngine.dll      ※ Unityエンジン
├── ColossalManaged.dll  ※ ゲーム基本機能
└── Assembly-CSharp.dll  ※ ゲームロジック
```

### CitiesHarmony

CitiesHarmonyはNuGetパッケージとして自動取得されます：
- `CitiesHarmony.API` (NuGet) - 開発時の参照用
- Workshop ID 2040656402のCitiesHarmony modが実行時に必要

## ビルド・配置

### 1. Steamパスの設定

環境変数 `STEAM_PATH` を設定するか、標準的な場所にSteamがインストールされている必要があります：

```powershell
# 環境変数で指定する場合（カスタムインストール場所の場合）
$env:STEAM_PATH = "C:\Program Files (x86)\SteamLibrary"
```

自動検出される場所：
- `C:\Program Files (x86)\SteamLibrary`
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
