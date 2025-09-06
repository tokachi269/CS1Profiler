# FPS_Booster MOD 技術調査報告書

## 調査概要
- **調査対象**: Cities: Skylines 1 FPS_Booster MOD
- **調査日**: 2025年9月6日
- **目的**: パフォーマンス分析結果での43.8%負荷の詳細解析
- **Steam Workshop ID**: 2105755179

## パフォーマンス分析結果
### 高負荷メソッド Top 5
1. `Unknown.RenderManager.FpsBoosterLateUpdate`: 112.85ms/frame (30.1%)
2. `Unknown.SimulationManager.FpsBoosterUpdate`: 37.49ms/frame (9.2%)
3. `Unknown.ThreadingWrapper.OnUpdate`: 37.45ms/frame (9.2%)
4. `PloppableAsphaltFix.MainThread.OnUpdate`: 37.32ms/frame (9.2%)
5. `Unknown.Building.RenderInstance`: 22.58ms/frame (6.7%)

### 分析データ詳細
- **総レコード数**: 24,177,498
- **総メソッド数**: 465
- **平均FPS**: 5.6
- **30FPS未満フレーム**: 278/282 (98.6%)

## FPS_Booster MODアーキテクチャ

### コンポーネント構成
```
FPS_Booster エコシステム
├── PatchLoader/ (エントリーポイント)
├── BehaviourPatcher/ (パッチング実行)
├── BehaviourUpdater/ (最適化エンジン)
├── AutoPatcher/ (自動パッチ適用)
├── Patch.API/ (IPatchインターフェース)
├── Utils/ (ユーティリティ)
└── Mono.Cecil/ (アセンブリ操作)
```

### 実装場所
- **インストール先**: `C:\Program Files (x86)\SteamLibrary\steamapps\workshop\content\255710\2105755179\`
- **パッチファイル**: `Patches\*.ipatch`
- **実行ファイル**: `Patches\*.dll`

## 動的メソッド名生成メカニズム

### パッチング処理
1. **BehaviourPatchACS.cs**が`MonoBehaviour`継承クラスを検索
2. **Util.cs**の`ApplyUpdateLateUpdateRenamePatches`がメソッド名を変更:
   ```csharp
   methodDefinition.Name = "FpsBoosterUpdate";        // Update() → FpsBoosterUpdate
   methodDefinition.Name = "FpsBoosterLateUpdate";    // LateUpdate() → FpsBoosterLateUpdate
   ```
3. **BehaviourRegistry**に登録して管理

### 対象クラス選定条件
```csharp
// BehaviourPatchACS.cs L28-38
(from t in assemblyDefinition.MainModule.Types
where t.ExtendAnyOfBaseClasses(new string[] { "MonoBehaviour" }) 
&& !t.Name.Equals("LoadingAnimation")
select t).ToList<TypeDefinition>().ForEach(...)
```

## 最適化戦略の実装

### 1. 選択的実行制御
**実装場所**: `BehaviourUpdater\Updater.cs`

#### Update処理 (L82-85)
```csharp
if (this._currentMonoBehaviour && this._currentMonoBehaviour.enabled)
{
    this._currentMonoBehaviour.FpsBoosterUpdate();
}
```

#### LateUpdate処理 (L154-158)
```csharp
if (this._currentMonoBehaviour && this._currentMonoBehaviour.enabled)
{
    this._currentMonoBehaviour.FpsBoosterLateUpdate();
}
```

### 2. 可視性ベース最適化
**実装場所**: `BehaviourUpdater\UiUpdater.cs`

#### 可視性チェック (L67-69)
```csharp
if (this.UpdateAll || (this._components[i].enabled && this._components[i].isVisible))
{
    // 可視状態のコンポーネントのみ更新
}
```

#### 階層トラバーサル (L98-105)
```csharp
if (component && component.fps_updateStarted && 
    (updateAll || component.isVisibleSelf || component.gameObject.name.Equals("BButton")))
{
    list.Add(component);
    component.FpsBoosterUpdate();
}
```

### 3. キャッシュベース実行
**実装場所**: `BehaviourUpdater\UiUpdater.cs`

#### キャッシュ管理 (L19, L76, L157)
```csharp
private FastList<UIComponent> _lateUpdateComponentCache;

// Update時にキャッシュに追加
this._lateUpdateComponentCache.Add(component);

// LateUpdate時にキャッシュから実行
this._lateUpdateComponentCache[i].FpsBoosterLateUpdate();
```

### 4. 集約実行管理
**実装場所**: `BehaviourUpdater\BehaviourRegistry.cs`

#### RenderManager特別管理 (L28-31, L103)
```csharp
if (behaviour.name.Equals("RenderManager"))
{
    BehaviourRegistry.RenderManagerBehaviour = behaviour;
}

internal static MonoBehaviour RenderManagerBehaviour;
```

## バニラコードとの関係

### 元のRenderManager.LateUpdate構造
**ファイル**: `Assembly-CSharp\RenderManager.cs` L378-L500

#### 主要処理フロー
1. フレーム管理 (`m_currentFrame += 1U`)
2. バッファクリア (`m_lightSystem.m_lightBuffer.Clear()`)
3. 情報モード更新 (`UpdateInfoMode()`)
4. カメラ情報更新 (`UpdateCameraInfo()`)
5. カラーマップ更新 (`UpdateColorMap()`)
6. レンダリング開始 (`BeginRendering()`)
7. レンダリンググループ処理 (45x45グリッド、最大2025ループ)
8. レンダリング終了 (`EndRendering()`)

### FPS_Boosterの処理方式
- **メソッド名変更のみ**: `LateUpdate` → `FpsBoosterLateUpdate`
- **処理内容は同一**: バニラの重いレンダリング処理をそのまま実行
- **最適化はスキップ制御**: 不要な処理の実行を回避

## 技術的制約と現状

### 最適化の本質
FPS_Boosterは「**処理をスキップする交通整理役**」であり、「**重い処理を軽くする最適化エンジン**」ではない。

### 43.8%負荷の理由
1. **スキップ不可能な処理**: カメラ情報更新、レンダリンググループ処理は必須
2. **可視状態の処理**: 画面に映っている要素は処理をスキップできない
3. **根本的重さ**: バニラの`LateUpdate`内のレンダリング処理そのものが重い

### 実装済み最適化
- ✅ `fps_updateStarted`フラグによる不要処理スキップ
- ✅ 可視性チェック（`isVisible`, `isVisibleSelf`）
- ✅ 集約実行によるオーバーヘッド削減
- ✅ キャッシュベースの効率実行

## ソースコード配置

### 参照用ソースコード
- `reference/BehaviourPatcher/`: パッチング実装
- `reference/BehaviourUpdater/`: 最適化エンジン実装  
- `reference/AutoPatcher/`: 自動パッチ適用実装
- `reference/Assembly-CSharp/RenderManager.cs`: バニラの元実装

### 重要ファイル
1. `BehaviourPatcher/Util.cs`: メソッド名変更実装
2. `BehaviourUpdater/Updater.cs`: 最適化実行制御
3. `BehaviourUpdater/UiUpdater.cs`: UI最適化実装
4. `BehaviourUpdater/BehaviourRegistry.cs`: コンポーネント管理

## 調査結論

### 技術的評価
- FPS_Boosterは高度な最適化を既に実装済み
- 現在の43.8%負荷は最適化後の結果
- さらなる最適化は技術的に非常に困難

### 代替最適化ターゲット
1. `PloppableAsphaltFix.MainThread.OnUpdate` (9.2%)
2. `Unknown.Building.RenderInstance` (6.7%)
3. `Unknown.NetSegment.RenderInstance` (3.4%)
4. `Unknown.NetNode.RenderInstance` (3.1%)

これらの最適化により、総合的なパフォーマンス向上が期待できる。
