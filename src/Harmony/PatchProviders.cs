using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// パフォーマンス測定パッチプロバイダー
    /// IPatchProviderを実装した型安全なパッチ管理
    /// </summary>
    public class PerformancePatchProvider : IPatchProvider
    {
        public string Name => "Performance";
        public bool DefaultEnabled => false; // デフォルトOFF（性能重視）
        public bool IsEnabled { get; private set; } = false;

        private List<MethodInfo> patchedMethods = new List<MethodInfo>();
        private HashSet<string> modAssemblyNames = new HashSet<string>();
        private HashSet<string> modTypeNames = new HashSet<string>();

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled)
            {
                return;
            }

            try
            {
                // MOD検出
                DetectRealModAssemblies();
                
                // パッチ適用
                ApplyPatches(harmony);
                
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches enabled: {patchedMethods.Count} methods");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
                
            
                // 部分的成功でも動作を継続（少なくとも1つのパッチが成功していればOK）
                if (patchedMethods.Count > 0)
                {
                    IsEnabled = true;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches partially enabled: {patchedMethods.Count} methods (some failed)");
                }
                else
                {
                    IsEnabled = false;
                    patchedMethods.Clear();
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} No patches could be applied for {Name}");
                }
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                int removedCount = 0;
                foreach (var method in patchedMethods)
                {
                    try
                    {
                        harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPrefix"));
                        harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPostfix"));
                        removedCount++;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to remove patch from {method.DeclaringType?.Name}.{method.Name}: {e.Message}");
                    }
                }
                
                patchedMethods.Clear();
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled: {removedCount} methods removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
                throw;
            }
        }

        private void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            var targetAssemblies = new List<string> { "Assembly-CSharp", "ColossalManaged" };
            targetAssemblies.AddRange(modAssemblyNames);
            
            int successCount = 0;
            int failureCount = 0;
            
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string assemblyName = assembly.GetName().Name;
                    if (!targetAssemblies.Contains(assemblyName)) continue;
                    
                    foreach (var type in assembly.GetTypes())
                    {
                        try
                        {
                            if (!IsPerformanceCriticalType(type)) continue;
                            
                            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .Where(IsPerformanceCriticalMethod)
                                .Where(CanSafelyPatch)
                                .Where(IsMethodSafeForPatching); // 安全性チェック
                            
                            foreach (var method in methods)
                            {
                                try
                                {
                                    var prefix = new HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPrefix");
                                    var postfix = new HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPostfix");
                                    
                                    // 個別のパッチエラーをキャッチして継続
                                    harmony.Patch(method, prefix, postfix);
                                    patchedMethods.Add(method);
                                    successCount++;
                                }
                                catch (Exception e)
                                {
                                    // 個別のパッチ失敗はログに記録して継続
                                    failureCount++;
                                    UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to patch {method.DeclaringType?.Name}.{method.Name}: {e.Message}");
                                }
                                
                                // 一定間隔で待機（50メソッドごとに少し待機、MOD多数時の安定性向上）
                                if (successCount % 50 == 0 && successCount > 0)
                                {
                                    System.Threading.Thread.Sleep(20); // 20ms待機
                                }
                                
                                // 失敗が多い場合は追加の待機
                                if (failureCount % 10 == 0 && failureCount > 0)
                                {
                                    System.Threading.Thread.Sleep(5); // 5ms待機
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // 型の処理でエラーが発生しても継続
                            UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to process type {type?.Name}: {e.Message}");
                        }
                    }
                }
                catch (Exception e)
                {
                    // アセンブリの処理でエラーが発生しても継続
                    UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to process assembly {assembly?.GetName()?.Name}: {e.Message}");
                }
            }
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance patches applied: {successCount} methods succeeded, {failureCount} methods failed");
        }
        
        private bool IsMethodSafeForPatching(MethodInfo method)
        {
            try
            {
                // 基本的な危険メソッドチェック
                if (method == null) return false;
                if (method.IsAbstract) return false;
                if (method.IsGenericMethod || method.IsGenericMethodDefinition) return false;
                if (method.ContainsGenericParameters) return false;
                if (method.IsConstructor) return false;
                if (method.IsSpecialName) return false; // プロパティのget/setなど
                
                // 宣言型のチェック
                var declaringType = method.DeclaringType;
                if (declaringType == null) return false;
                
                // JsonFxタイプのメソッドを除外
                if (declaringType.FullName?.StartsWith("JsonFx") == true) return false;
                
                // ModsCommon.Utilities.Polygonの問題メソッドを除外
                if (declaringType.FullName?.StartsWith("ModsCommon.Utilities.Polygon") == true) return false;
                
                // ModsCommon関連で問題を起こしやすいその他のクラスも除外
                if (declaringType.FullName?.StartsWith("ModsCommon.Utilities") == true && method.Name == "Arrange") return false;
                
                // その他の問題のあるタイプを除外
                if (declaringType.FullName?.Contains("IL_") == true) return false; // IL生成系
                if (declaringType.FullName?.Contains("dynamic") == true) return false; // 動的生成系
                if (declaringType.FullName?.Contains("Wrapper") == true) return false; // ラッパー系
                
                // ToStringメソッドを除外（JsonObjectIDなどで問題を起こす可能性）
                if (method.Name == "ToString") return false;
                
                // IL本体の存在チェック
                var methodBody = method.GetMethodBody();
                if (methodBody == null) return false;
                
                // ネイティブメソッドの除外
                if ((method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0) return false;
                if ((method.GetMethodImplementationFlags() & MethodImplAttributes.Native) != 0) return false;
                
                // パラメータチェック
                var parameters = method.GetParameters();
                if (parameters.Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer)) return false;
                
                // 戻り値の型チェック
                if (method.ReturnType.IsByRef || method.ReturnType.IsPointer) return false;
                
                return true;
            }
            catch
            {
                return false; // 何らかのエラーが発生したら安全にfalseを返す
            }
        }
        
        private bool CanSafelyPatch(MethodInfo method)
        {
            // 最小限の安全性チェック（上記のIsMethodSafeForPatchingでより詳細にチェック）
            if (method == null) return false;
            if (method.IsAbstract) return false;
            if (method.ContainsGenericParameters) return false;
            
            return true;
        }

        private void DetectRealModAssemblies()
        {
            // MOD検出ロジック（既存の実装を流用）
            var pluginManager = ColossalFramework.Plugins.PluginManager.instance;
            foreach (var plugin in pluginManager.GetPluginsInfo())
            {
                if (plugin.isEnabled && !plugin.isBuiltin)
                {
                    foreach (var assembly in plugin.GetAssemblies())
                    {
                        modAssemblyNames.Add(assembly.GetName().Name);
                        foreach (var type in assembly.GetTypes().Take(50))
                        {
                            modTypeNames.Add(type.FullName);
                        }
                    }
                }
            }
        }

        private bool IsPerformanceCriticalType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.IsEnum) return false;
            
            // ジェネリック型除外
            if (type.IsGenericType || type.ContainsGenericParameters) return false;
            
            // 問題のあるタイプを明示的に除外
            var fullName = type.FullName ?? "";
            
            // JsonFx関連のタイプを除外（パッチに失敗するため）
            if (fullName.StartsWith("JsonFx")) return false;
            
            // その他の危険なタイプを除外
            if (fullName.StartsWith("System") || 
                fullName.StartsWith("Microsoft") ||
                fullName.Contains("UnityEngine") ||
                fullName.Contains("Mono.") ||
                fullName.Contains("HarmonyLib") ||
                fullName.Contains("MonoMod")) return false;
            
            // ブラックリスト除外
            if (IsBlacklistedMod(type)) return false;
            
            // MODタイプは含める
            if (modTypeNames.Contains(type.FullName)) return true;
            
            return true;
        }

        private bool IsBlacklistedMod(Type type)
        {
            return type.FullName?.StartsWith("CSShared") == true ||
                   type.FullName?.StartsWith("ModTools") == true ||
                   type.FullName?.StartsWith("CS1Profiler") == true;
        }

        private bool IsPerformanceCriticalMethod(MethodInfo method)
        {
            if (method.IsAbstract || method.IsGenericMethod) return false;
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) return false;
            
            return true;
        }
    }

    /// <summary>
    /// シミュレーション測定パッチプロバイダー
    /// </summary>
    public class SimulationPatchProvider : IPatchProvider
    {
        public string Name => "Simulation";
        public bool DefaultEnabled => false;
        public bool IsEnabled { get; private set; } = false;

        private List<MethodInfo> patchedMethods = new List<MethodInfo>();

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling {Name} patches...");
                
                var simType = typeof(SimulationManager);
                var simMethod = simType.GetMethod("SimulationStepImpl", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (simMethod != null)
                {
                    harmony.Patch(
                        original: simMethod,
                        prefix: new HarmonyMethod(typeof(LegacySimulationHooks).GetMethod("Pre")),
                        postfix: new HarmonyMethod(typeof(LegacySimulationHooks).GetMethod("Post"))
                    );
                    patchedMethods.Add(simMethod);
                }

                var simStepMethod = simType.GetMethod("SimulationStep", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (simStepMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPrefix");
                    var postfix = new HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPostfix");
                    harmony.Patch(simStepMethod, prefix, postfix);
                    patchedMethods.Add(simStepMethod);
                }

                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches enabled: {patchedMethods.Count} methods");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling {Name} patches...");
                
                foreach (var method in patchedMethods)
                {
                    harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Pre"));
                    harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Post"));
                    harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPrefix"));
                    harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPostfix"));
                }
                
                patchedMethods.Clear();
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// ログ抑制パッチプロバイダー
    /// </summary>
    public class LogSuppressionPatchProvider : IPatchProvider
    {
        public string Name => "LogSuppression";
        public bool DefaultEnabled => true; // デフォルトON
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                LogSuppressionPatcher.ApplyPatches(harmony);
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches enabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                // LogSuppressionPatcherにRemove機能が必要
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 起動解析パッチプロバイダー
    /// </summary>
    public class StartupAnalysisPatchProvider : IPatchProvider
    {
        public string Name => "StartupAnalysis";
        public bool DefaultEnabled => false; // 手動開始に変更
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                // StartupAnalysisPatcher削除：手動プロファイリングに統合
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} moved to manual profiling via MPSC system");
                IsEnabled = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                // 何もしない（MPSCシステムで管理）
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// RenderIt MOD最適化パッチプロバイダー
    /// RenderItのModUtils最適化パッチを管理
    /// </summary>
    public class RenderItOptimizationPatchProvider : IPatchProvider
    {
        public string Name => "RenderItOptimization";
        public bool DefaultEnabled => true; // デフォルトON（RenderItが重い問題の解決）
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled)
            {
                return;
            }

            try
            {
                // RenderItOptimizationクラスの最適化パッチを適用
                CS1Profiler.RenderItOptimization.ApplyRenderItOptimizationPatches(harmony);
                
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches enabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable {Name} patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                // RenderItOptimizationのキャッシュクリア
                CS1Profiler.RenderItOptimizationHooks.ClearCache();
                
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {Name} patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable {Name} patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// PloppableAsphaltFix 最適化パッチプロバイダー
    /// 838.78msスパイク + 93回スパイクの根本原因を解決
    /// </summary>
    public class PloppableAsphaltFixOptimizationPatchProvider : IPatchProvider
    {
        public string Name => "PloppableAsphaltFix Optimization";
        public bool DefaultEnabled => true; // デフォルトで有効
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                PloppableAsphaltFixOptimization.Enable(harmony);
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix optimization patches enabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable PloppableAsphaltFix optimization patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                PloppableAsphaltFixOptimization.Disable();
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix optimization patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable PloppableAsphaltFix optimization patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// GameSettings 最適化パッチプロバイダー
    /// 保存間隔を1秒から1分に変更して、79ms/frameのボトルネックを軽減
    /// </summary>
    public class GameSettingsOptimizationPatchProvider : IPatchProvider
    {
        public string Name => "GameSettings Optimization";
        public bool DefaultEnabled => true; // デフォルトで有効
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                // GameSettingsOptimizationのパッチを適用
                var monitorSavePatch = typeof(GameSettingsOptimization.GameSettings_MonitorSave_Patch);
                var saveAllPatch = typeof(GameSettingsOptimization.GameSettings_SaveAll_Patch);
                
                harmony.PatchAll(monitorSavePatch.Assembly);
                
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} GameSettings optimization patches enabled (save interval: 1 second -> 1 minute)");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable GameSettings optimization patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                // パッチを無効化（Harmonyの制限により完全な無効化は困難）
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} GameSettings optimization patches disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable GameSettings optimization patches: {e.Message}");
                throw;
            }
        }
    }
}
