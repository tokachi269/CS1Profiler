# CS1Profiler - Harmony Patches Architecture

## 📁 フォルダ構成

CS1Profilerの**Harmonyパッチシステム**の責任分担による設計です。

## 🏗️ ファイル構成と責任

### 🎮 **統合制御層**
| ファイル | 責任 | 主要クラス |
|---------|------|-----------|
| `HarmonyPatches.cs` | **パッチ統合管理** | `Patcher` (PatchAll/UnpatchAll) |

### 🔍 **機能実装層**
| ファイル | 責任 | 対象 |
|---------|------|------|
| `PerformancePatches.cs` | **汎用パフォーマンス測定** | MODアセンブリ全体の実行時間計測 |
| `StartupPatches.cs` | **起動シーケンス解析** | BootStrapper, PackageManager, LoadingExtension |
| `RenderItOptimization.cs` | **特定MOD最適化** | RenderIt ModUtils キャッシュ最適化 |
| `HookClasses.cs` | **Hook実装** | 各パッチの前後処理実装 |

## 🔄 アーキテクチャフロー

```
UserMod.OnEnabled()
    ↓
CS1Profiler.Patcher.PatchAll()
    ↓
┌─ PerformancePatches.ApplyPerformancePatches()
├─ StartupPatches.PatchStartupMethods()
├─ RenderItOptimization.ApplyRenderItOptimizationPatches()
└─ ApplySimulationManagerPatch()
```

## 📋 各ファイル詳細

### HarmonyPatches.cs (107行)
- **役割**: 全パッチシステムの制御塔
- **主要メソッド**: `PatchAll()`, `UnpatchAll()`
- **特徴**: SimulationManager専用パッチも含む

### PerformancePatches.cs (251行)
- **役割**: 汎用パフォーマンス測定システム
- **除外フィルター**: `EXCLUDED_PATTERNS` (get_, set_, __ など)
- **出力形式**: `Namespace.ClassName.MethodName`
- **Hook**: `PerformanceHooks` (ProfilerPrefix/Postfix)

### StartupPatches.cs (139行)
- **役割**: Cities: Skylines起動シーケンス解析
- **対象**: BootStrapper, PackageManager, LoadingExtension
- **Hook**: `StartupHooks` (起動イベントログ)

### RenderItOptimization.cs (171行)
- **役割**: RenderIt MODの重処理最適化
- **最適化対象**: `ModUtils.IsModEnabled()`, `ModUtils.IsAnyModsEnabled()`
- **手法**: Dictionaryキャッシュによる高速化

### HookClasses.cs (115行)
- **役割**: 実際のHook実装クラス群
- **含有クラス**: `StartupHooks`, `LogSuppressionHooks`

## 🎯 命名規則

| パターン | 用途 | 例 |
|----------|------|-----|
| `*Patches.cs` | パッチ制御・管理 | PerformancePatches, StartupPatches |
| `*Optimization.cs` | 特定最適化 | RenderItOptimization |
| `*Hooks.cs` | Hook実装 | HookClasses |

## 🔧 開発ガイドライン

### 新機能追加時
1. 機能別にファイル分割 (`*Patches.cs`)
2. `HarmonyPatches.cs`の`PatchAll()`に統合
3. 実装は対応する`*Hooks`クラスに配置

### パフォーマンス考慮
- `EXCLUDED_PATTERNS`で不要メソッド除外
- `IsEnabled()`チェックで無効時はスキップ
- try-catchでエラー耐性確保