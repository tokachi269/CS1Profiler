# RenderManagerOptimization クラス ドキュメント

## 概要
`RenderManagerOptimization`は、Cities: SkylinesのRenderManager.LateUpdate()系メソッドの性能分析と最適化を行うクラスです。

## 目的
- RenderManager.LateUpdate()の実行時間測定
- FPS Booster MODとバニラゲーム両方への対応
- 将来的な最適化パッチの基盤提供

## 対象メソッド

### 1. RenderManager.FpsBoosterLateUpdate()
- **対象**: FPS Booster MOD導入時
- **パッチ方式**: Prefix/Postfix
- **機能**: 実行時間測定、スパイク検出

### 2. RenderManager.LateUpdate()
- **対象**: バニラゲーム時
- **パッチ方式**: Transpiler（完全置換）
- **機能**: オリジナルロジックを最適化版で置換

## 主要機能

### デュアルメソッドパッチング
```csharp
// FPS Booster MOD検出時
if (fpsBoosterLateUpdateMethod != null)
{
    _harmony.Patch(fpsBoosterLateUpdateMethod, prefixMethod, postfixMethod);
}

// バニラゲーム時
if (lateUpdateMethod != null)
{
    _harmony.Patch(lateUpdateMethod, transpiler: transpilerMethod);
}
```

### パフォーマンスキャッシュ
リフレクション結果をキャッシュして高速化：
- `Type` オブジェクト
- `FieldInfo` / `PropertyInfo` / `MethodInfo`
- 初期化時1回のみ実行、実行時は高速アクセス

### 実装済み機能
1. **PrefabPool.m_canCreateInstances = 1**
   - プレハブ生成許可フラグ設定
2. **InfoManager.UpdateInfoMode()**
   - 情報表示モード更新
3. **LoadingManager完了チェック**
   - ロード中は処理をスキップ

## API仕様

### 主要メソッド

#### Enable(HarmonyLib.Harmony harmony)
- **目的**: RenderManager最適化パッチを有効化
- **処理**:
  1. リフレクションキャッシュ初期化
  2. デュアルメソッドパッチ適用
  3. ログ出力

#### Disable()
- **目的**: パッチを無効化
- **処理**:
  1. 適用済みパッチをすべて解除
  2. 状態リセット

#### OptimizedLateUpdate(object renderManagerInstance)
- **目的**: 最適化されたLateUpdateロジック実行
- **引数**: RenderManagerインスタンス（dynamic型）
- **処理**: オリジナルLateUpdate()の完全実装

### プロファイリングメソッド

#### FpsBoosterLateUpdatePrefix/Postfix
- **Prefix**: 実行開始時刻記録
- **Postfix**: 実行時間計算、スパイク検出（200ms超過で警告）

## 技術仕様

### Harmonyパッチ方式
- **Prefix/Postfix**: FpsBoosterLateUpdate用（非破壊的分析）
- **Transpiler**: LateUpdate用（完全置換）

### リフレクション最適化
```csharp
// キャッシュ変数
private static Type _prefabPoolType;
private static FieldInfo _canCreateInstancesField;
private static Type _infoManagerType;
// ... 他のキャッシュ変数

// 初期化時1回のみ実行
private static void InitializeCaches()
{
    _prefabPoolType = GetTypeFromAssembly("PrefabPool");
    _canCreateInstancesField = _prefabPoolType.GetField("m_canCreateInstances", ...);
    // ... キャッシュ設定
}
```

### エラーハンドリング
- すべてのリフレクション操作をtry-catch包囲
- 失敗時はログ出力して継続実行
- パッチ適用失敗時は例外throw

## 使用方法

### 基本的な使用例
```csharp
// 有効化
RenderManagerOptimization.Enable(harmony);

// 状態確認
if (RenderManagerOptimization.IsEnabled)
{
    // パッチが適用されている
}

// 無効化
RenderManagerOptimization.Disable();
```

### 統合例（PatchControllerから）
```csharp
public static void EnableRenderManagerPatch()
{
    if (!RenderManagerOptimization.IsEnabled)
    {
        RenderManagerOptimization.Enable(_harmony);
    }
}
```

## ログ出力仕様

### 情報ログ
- `"RenderManager analysis patch enabled"`
- `"RenderManager.FpsBoosterLateUpdate analysis patch applied"`
- `"RenderManager.LateUpdate optimization transpiler applied"`

### 警告ログ
- `"FpsBoosterLateUpdate spike detected: XXXms"` (200ms超過時)
- `"RenderManager type not found - RenderManager patch unavailable"`

### エラーログ
- `"Failed to enable RenderManager patch: [エラー詳細]"`
- `"Failed to patch RenderManager.FpsBoosterLateUpdate: [エラー詳細]"`

## 注意事項

### 制限事項
- Assembly-CSharpからのみType検索実行
- dynamic型使用によるランタイムエラー可能性
- MOD環境での動作保証なし

### 既知の問題
- 一部のMODでRenderManagerが改変されている場合の非互換性
- リフレクション失敗時のフォールバック機能なし

## 将来の拡張予定

### 最適化機能
- レンダリンググループ処理の並列化
- カメラ範囲外オブジェクトの早期カリング
- LOD距離計算の最適化

### 分析機能
- フレーム毎の詳細プロファイル出力
- ボトルネック箇所の自動特定
- リアルタイム最適化提案

## 関連ファイル
- `src/Harmony/RenderManagerOptimization.cs` - メインクラス
- `src/Core/PatchController.cs` - パッチ統合管理
- `src/Core/Constants.cs` - ログプレフィックス定義