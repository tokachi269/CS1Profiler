# Cities: Skylines 1 起動性能解析ガイド

## 概要
CS1 Startup Performance Analyzerは、Cities: Skylines 1の起動時のボトルネックを特定し、性能問題を解決するために開発された詳細な分析ツールです。

## 機能

### 1. 起動フェーズの詳細トラッキング
- ゲーム起動から初回メニュー表示まで全ての重要なイベントを記録
- 各フェーズの実行時間を正確に測定
- メモリ使用量の変化を追跡

### 2. 重要なボトルネック分析
- **BootStrapper.Boot()**: ゲーム核心エンジンの初期化
- **PackageManager.Ensure()**: MOD/アセット読み込み処理
- **LoadingExtension**: 各MODの初期化処理

### 3. リアルタイム監視
- CSVファイルへの詳細ログ出力
- キーボードショートカットでのリアルタイム制御
- 画面上での統計情報表示

## インストール方法

1. ビルドされたDLLファイルを取得：
   ```
   CS1Profiler\output\CS1Profiler.dll
   ```

2. Cities: SkylinesのLocal Modsフォルダに配置：
   ```
   %LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\
   ```

3. フォルダ構造：
   ```
   CS1Profiler/
   ├── CS1Profiler.dll
   └── (他のファイルは不要)
   ```

## 使用方法

### 基本的な起動解析
1. MODを有効にしてCities: Skylinesを起動
2. ゲーム起動中、コンソールに詳細なログが出力されます
3. メインメニューが表示されたら、起動解析レポートが生成されます

### キーボード操作
- **P**: プロファイリングのON/OFF切り替え
- **L**: ログ表示のON/OFF切り替え
- **R**: 現在の統計情報を表示（FPS、メモリ使用量）

### 出力ファイル
解析結果は以下の場所に保存されます：
```
Documents\My Games\Cities_Skylines\CS1Profiler_startup_YYYYMMDD_HHMMSS.csv
```

## 分析データの見方

### CSV出力フォーマット
```
DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description
```

### 重要なイベントタイプ
- **MOD_ENABLED**: プロファイラー自体の初期化
- **HARMONY_INIT_**: Harmonyパッチシステムの初期化
- **BOOTSTRAPPER_BOOT_**: ゲームエンジンの核心初期化
- **PACKAGEMANAGER_ENSURE_**: MOD/アセットの読み込み
- **LOADING_ONCREATED_**: 各MODの初期化
- **LOADING_ONLEVELLOAD_**: レベル読み込み処理

### パフォーマンス問題の特定方法

#### 1. 起動が遅い原因の特定
```
BOOTSTRAPPER_BOOT_END を検索 → 基本エンジン初期化の時間
PACKAGEMANAGER_ENSURE_END を検索 → MOD読み込み時間
```

#### 2. 特定MODの影響調査
```
LOADING_ONCREATED_ または LOADING_ONLEVELLOAD_ イベントで
Duration(ms) が大きいMODを特定
```

#### 3. メモリリークの検出
```
MemoryMB カラムで起動中のメモリ使用量の変化を確認
急激な増加があるイベントを特定
```

## よくある問題と解決方法

### 問題1: 起動時にクラッシュする
**原因**: MODの互換性問題またはメモリ不足
**解決**: 
1. 他のMODを一時的に無効化
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
