# Harmonyパッチ開発ガイド

CS1Profiler プロジェクトにおける新しいHarmonyパッチの実装手順を説明します。

## 概要

このプロジェクトでは**IPatchProvider**インターフェースを使用した型安全なパッチ管理システムを採用しています。これにより、パッチの有効化/無効化、エラーハンドリング、設定管理が統一的に行われます。

## プロジェクト構造

```
src/Harmony/
├── IPatchProvider.cs          # パッチプロバイダーインターフェース
├── PatchController.cs         # パッチ管理の中心クラス
├── PatchProviders.cs          # 各パッチプロバイダーの実装
├── GameSettingsOptimization.cs # パッチ実装例
├── RenderManagerOptimization.cs
└── [その他のパッチファイル]
```

## 新規パッチ実装手順

### Step 1: パッチクラスの作成

`src/Harmony/` フォルダに新しいパッチファイルを作成します：

```csharp
// Example: MyFeaturePatch.cs
using HarmonyLib;
using UnityEngine;
using CS1Profiler.Core;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// 新機能のパッチ実装
    /// 目的: [パッチの目的を明確に記載]
    /// 対象: [対象クラス・メソッドを記載]
    /// </summary>
    public class MyFeaturePatch
    {
        /// <summary>
        /// [対象メソッド]にパッチを適用
        /// 効果: [期待される効果を記載]
        /// </summary>
        [HarmonyPatch(typeof(TargetClass), "TargetMethod")]
        public class TargetClass_TargetMethod_Patch
        {
            // Prefixパッチ（元メソッド実行前）
            static bool Prefix(/* 必要なパラメータ */)
            {
                try
                {
                    // 前処理ロジック
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyFeature patch: Prefix executed");
                    return true; // true: 元メソッドを実行, false: スキップ
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} MyFeature patch error: {e.Message}");
                    return true; // エラー時は元メソッドを実行
                }
            }

            // Postfixパッチ（元メソッド実行後）
            static void Postfix(/* 必要なパラメータ */)
            {
                try
                {
                    // 後処理ロジック
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyFeature patch: Postfix executed");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} MyFeature patch postfix error: {e.Message}");
                }
            }
        }

        /// <summary>
        /// パッチ適用メソッド（静的メソッドとして実装）
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                // 手動パッチ適用（より細かい制御が必要な場合）
                var targetMethod = typeof(TargetClass).GetMethod("TargetMethod");
                if (targetMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(TargetClass_TargetMethod_Patch), "Prefix");
                    var postfix = new HarmonyMethod(typeof(TargetClass_TargetMethod_Patch), "Postfix");
                    harmony.Patch(targetMethod, prefix, postfix);
                }
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyFeature patches applied successfully");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply MyFeature patches: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// パッチ削除メソッド
        /// </summary>
        public static void RemovePatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                var targetMethod = typeof(TargetClass).GetMethod("TargetMethod");
                if (targetMethod != null)
                {
                    harmony.Unpatch(targetMethod, HarmonyPatchType.All, Constants.HARMONY_ID);
                }
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyFeature patches removed successfully");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove MyFeature patches: {e.Message}");
            }
        }
    }
}
```

### Step 2: IPatchProviderの実装

`src/Harmony/PatchProviders.cs` に新しいパッチプロバイダーを追加：

```csharp
/// <summary>
/// 新機能パッチプロバイダー
/// [機能の説明を記載]
/// </summary>
public class MyFeaturePatchProvider : IPatchProvider
{
    public string Name => "MyFeature";
    public bool DefaultEnabled => false; // デフォルトで有効にするかどうか
    public bool IsEnabled { get; private set; } = false;

    private List<MethodInfo> patchedMethods = new List<MethodInfo>();

    public void Enable(HarmonyLib.Harmony harmony)
    {
        if (IsEnabled)
        {
            return; // 既に有効な場合はスキップ
        }

        try
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling {Name} patches...");
            
            // パッチ適用ロジック
            MyFeaturePatch.ApplyPatches(harmony);
            
            IsEnabled = true;
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches enabled successfully");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
            
            // 部分的成功でも動作継続を検討（必要に応じて）
            if (patchedMethods.Count > 0)
            {
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches partially enabled: {patchedMethods.Count} methods");
            }
            else
            {
                throw; // 完全に失敗した場合は例外を再スロー
            }
        }
    }

    public void Disable(HarmonyLib.Harmony harmony)
    {
        if (!IsEnabled)
        {
            return; // 既に無効な場合はスキップ
        }

        try
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling {Name} patches...");
            
            // パッチ削除ロジック
            MyFeaturePatch.RemovePatches(harmony);
            
            patchedMethods.Clear();
            IsEnabled = false;
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled successfully");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
            throw;
        }
    }
}
```

### Step 3: PatchControllerへの統合

`src/Harmony/PatchController.cs` に新しいプロバイダーを統合：

```csharp
public static class PatchController
{
    // 新しいプロバイダーのインスタンスを追加
    private static readonly MyFeaturePatchProvider _myFeatureProvider = new MyFeaturePatchProvider();

    /// <summary>
    /// 新機能パッチの有効/無効制御プロパティ
    /// </summary>
    public static bool MyFeatureEnabled
    {
        get => _myFeatureProvider.IsEnabled;
        set 
        {
            if (_initialized)
                SetPatchEnabled(_myFeatureProvider, value);
        }
    }

    // ApplyDefaultPatches()メソッド内でデフォルト適用を追加
    private static void ApplyDefaultPatches()
    {
        try
        {
            // ... 既存のデフォルトパッチ ...
            
            // 新機能のデフォルト有効化
            if (_myFeatureProvider.DefaultEnabled)
            {
                _myFeatureProvider.Enable(_harmony);
            }
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Default patches applied");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error applying default patches: {e.Message}");
        }
    }

    // DisablePatches()メソッドにも追加
    public static void DisablePatches()
    {
        try
        {
            // ... 既存の無効化処理 ...
            
            // 新機能の無効化
            if (_myFeatureProvider.IsEnabled)
            {
                MyFeatureEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyFeature disabled");
            }
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} All patches disabled successfully");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error disabling patches: {e.Message}");
        }
    }
}
```

## パッチタイプ別実装例

### 1. Prefixパッチ（メソッド実行前処理）

```csharp
[HarmonyPatch(typeof(BuildingManager), "CreateBuilding")]
public class BuildingManager_CreateBuilding_Patch
{
    static bool Prefix(ref bool __result, out Building building, /* その他パラメータ */)
    {
        building = default;
        
        // 条件チェック
        if (SomeConditionToSkip())
        {
            __result = false; // 戻り値を設定
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} CreateBuilding skipped due to condition");
            return false; // 元メソッドをスキップ
        }
        
        // パフォーマンス測定開始など
        PerformanceTracker.StartTimer("CreateBuilding");
        
        return true; // 元メソッドを実行
    }
}
```

### 2. Postfixパッチ（メソッド実行後処理）

```csharp
[HarmonyPatch(typeof(SimulationManager), "SimulationStep")]
public class SimulationManager_SimulationStep_Patch
{
    static void Postfix()
    {
        try
        {
            // シミュレーションステップ後の統計収集
            PerformanceTracker.EndTimer("SimulationStep");
            
            // カスタム処理
            CustomSimulationHandler.ProcessStep();
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} SimulationStep postfix completed");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} SimulationStep postfix error: {e.Message}");
        }
    }
}
```

### 3. Transpilerパッチ（ILコード変更）

```csharp
[HarmonyPatch(typeof(GameSettings), "MonitorSave")]
public class GameSettings_MonitorSave_Patch
{
    /// <summary>
    /// Monitor.Wait(m_LockObject, 1000)を Monitor.Wait(m_LockObject, 60000)に変更
    /// 保存間隔を1秒から1分に延長してパフォーマンスを改善
    /// </summary>
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        bool found = false;

        for (int i = 0; i < codes.Count; i++)
        {
            var instruction = codes[i];
            
            // 特定の定数値を検索
            if (instruction.opcode == OpCodes.Ldc_I4 && 
                instruction.operand is int value && value == 1000)
            {
                // 次の命令がMonitor.Waitの呼び出しかチェック
                if (i + 1 < codes.Count && 
                    codes[i + 1].opcode == OpCodes.Call)
                {
                    var nextInstruction = codes[i + 1];
                    if (nextInstruction.operand is MethodInfo methodInfo &&
                        methodInfo.DeclaringType == typeof(Monitor) &&
                        methodInfo.Name == "Wait")
                    {
                        // 値を置き換え
                        codes[i] = new CodeInstruction(OpCodes.Ldc_I4, 60000);
                        found = true;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} GameSettings: Wait interval changed from 1000ms to 60000ms");
                        break;
                    }
                }
            }
        }

        if (!found)
        {
            UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} GameSettings: Target pattern not found for transpiler");
        }

        return codes;
    }
}
```

## 安全な実装のための注意点

### 1. 例外処理

```csharp
// 常にtry-catchで例外をキャッチ
static bool Prefix()
{
    try
    {
        // パッチロジック
        return true;
    }
    catch (System.Exception e)
    {
        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Patch error: {e.Message}");
        return true; // エラー時は元メソッドを実行
    }
}
```

### 2. メソッド存在チェック

```csharp
public static void ApplyPatches(HarmonyLib.Harmony harmony)
{
    // メソッドが存在するかチェック
    var targetMethod = typeof(TargetClass).GetMethod("TargetMethod", 
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    if (targetMethod != null)
    {
        harmony.Patch(targetMethod, prefix: new HarmonyMethod(typeof(MyPatch), "Prefix"));
        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Patch applied to {targetMethod.Name}");
    }
    else
    {
        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Target method not found: TargetClass.TargetMethod");
    }
}
```

### 3. 型安全性の確保

```csharp
/// <summary>
/// パッチ適用が安全かどうかをチェック
/// </summary>
private static bool IsSafeToPath(MethodInfo method)
{
    if (method == null) return false;
    if (method.IsAbstract) return false;
    if (method.IsGenericMethod) return false;
    if (method.ContainsGenericParameters) return false;
    if (method.GetMethodBody() == null) return false;
    
    // 危険なタイプを除外
    var declaringType = method.DeclaringType;
    if (declaringType?.FullName?.StartsWith("System") == true) return false;
    if (declaringType?.FullName?.StartsWith("UnityEngine") == true) return false;
    
    return true;
}
```

### 4. パフォーマンス考慮

```csharp
// 頻繁に呼ばれるメソッドでは軽量な処理を心がける
static bool Prefix()
{
    // 重い処理は避ける
    if (ShouldSkip()) // 軽量なチェック
    {
        return false;
    }
    
    // 必要最小限の処理のみ
    IncrementCounter(); // 軽量な操作
    
    return true;
}
```

## テストとデバッグ

### 1. ログ出力による動作確認

```csharp
static bool Prefix()
{
    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyPatch.Prefix called at {DateTime.Now:HH:mm:ss.fff}");
    return true;
}

static void Postfix()
{
    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MyPatch.Postfix called at {DateTime.Now:HH:mm:ss.fff}");
}
```

### 2. 条件付き有効化

```csharp
public class MyFeaturePatchProvider : IPatchProvider
{
    public bool DefaultEnabled => 
        CS1Profiler.Settings.ModSettings.Instance?.MyFeatureEnabled ?? false;
}
```

### 3. デバッグモード対応

```csharp
#if DEBUG
    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Debug: MyPatch details - param1={param1}, param2={param2}");
#endif
```

## 既存パッチの参考例

プロジェクト内の既存パッチを参考にしてください：

- `GameSettingsOptimization.cs` - Transpilerパッチの例
- `PloppableAsphaltFixOptimization.cs` - 最適化パッチの例  
- `RenderManagerOptimization.cs` - 複雑なパフォーマンス分析パッチの例
- `BuildingRenderAnalysisPatcher.cs` - 詳細分析パッチの例

## 使用方法

パッチを実装した後の使用方法：

```csharp
// プログラムから
CS1Profiler.Harmony.PatchController.MyFeatureEnabled = true;

// 初期化時の自動適用
CS1Profiler.Harmony.PatchController.Initialize();

// 設定ファイルとの連携
CS1Profiler.Settings.ModSettings.Instance.MyFeatureEnabled = true;
```

## まとめ

この手順に従うことで：

1. **型安全性** - IPatchProviderによる統一的な管理
2. **エラー処理** - 例外時の適切なハンドリング
3. **設定連携** - 有効/無効の制御可能
4. **保守性** - 一貫した実装パターン
5. **デバッグ性** - 適切なログ出力

を備えた高品質なHarmonyパッチを実装できます。

既存のパッチ実装を参考にしながら、プロジェクトの設計方針に合わせて開発を進めてください。