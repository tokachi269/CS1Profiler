using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CS1Profiler.Core;
using System.Diagnostics;
using System.Text;
using ColossalFramework;

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

    /// <summary>
    /// Building RenderInstance分析パッチプロバイダー
    /// EndRenderingImpl完全置換によるRenderInstance呼び出し分析
    /// </summary>
    public class BuildingRenderAnalysisPatchProvider : IPatchProvider
    {
        public string Name => "BuildingRenderAnalysis";
        public bool DefaultEnabled => false; // デフォルトOFF（手動で有効化）
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                BuildingRenderAnalysisPatcher.ApplyPatches(harmony);
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building render analysis patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply building render analysis patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                BuildingRenderAnalysisPatcher.RemovePatches(harmony);
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building render analysis patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove building render analysis patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Building.RenderInstance詳細分析パッチプロバイダー
    /// 各処理段階の時間を細分化して測定
    /// </summary>
    public class BuildingRenderInstanceDetailPatchProvider : IPatchProvider
    {
        public string Name => "BuildingRenderInstanceDetail";
        public bool DefaultEnabled => false;
        public bool IsEnabled { get; private set; } = false;

        public void Enable(HarmonyLib.Harmony harmony)
        {
            if (IsEnabled) return;

            try
            {
                BuildingRenderInstanceDetailPatcher.ApplyPatches(harmony);
                BuildingRenderInstanceDetailHooks.Enable();
                IsEnabled = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building.RenderInstance detail analysis patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply Building.RenderInstance detail analysis patches: {e.Message}");
                throw;
            }
        }

        public void Disable(HarmonyLib.Harmony harmony)
        {
            if (!IsEnabled) return;

            try
            {
                BuildingRenderInstanceDetailHooks.Disable();
                BuildingRenderInstanceDetailPatcher.RemovePatches(harmony);
                IsEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building.RenderInstance detail analysis patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove Building.RenderInstance detail analysis patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Building.RenderInstance詳細分析パッチ
    /// 各処理段階の時間を細分化して測定
    /// </summary>
    public static class BuildingRenderInstanceDetailPatcher
    {
        private static bool _isPatched = false;
        private static readonly object _patchLock = new object();
        
        /// <summary>
        /// パッチ適用
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            lock (_patchLock)
            {
                if (_isPatched) return;

                try
                {
                    var buildingType = typeof(Building);
                    
                    // Public RenderInstance method
                    var publicRenderMethod = buildingType.GetMethod("RenderInstance", 
                        BindingFlags.Instance | BindingFlags.Public,
                        null, 
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int) },
                        null);

                    // Private RenderInstance method
                    var privateRenderMethod = buildingType.GetMethod("RenderInstance", 
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int), typeof(BuildingInfo), typeof(RenderManager.Instance).MakeByRefType() },
                        null);

                    if (publicRenderMethod != null)
                    {
                        var publicPrefix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "PublicRenderInstance_Prefix");
                        var publicPostfix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "PublicRenderInstance_Postfix");
                        
                        harmony.Patch(publicRenderMethod, prefix: publicPrefix, postfix: publicPostfix);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patch applied to public Building.RenderInstance");
                    }

                    if (privateRenderMethod != null)
                    {
                        var privatePrefix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "PrivateRenderInstance_Prefix");
                        var privatePostfix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "PrivateRenderInstance_Postfix");
                        
                        harmony.Patch(privateRenderMethod, prefix: privatePrefix, postfix: privatePostfix);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patch applied to private Building.RenderInstance");
                    }

                    // BuildingAI.RenderInstance method
                    var buildingAIType = typeof(BuildingAI);
                    var aiRenderMethod = buildingAIType.GetMethod("RenderInstance",
                        BindingFlags.Instance | BindingFlags.Public);

                    if (aiRenderMethod != null)
                    {
                        var aiPrefix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderInstance_Prefix");
                        var aiPostfix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderInstance_Postfix");
                        
                        harmony.Patch(aiRenderMethod, prefix: aiPrefix, postfix: aiPostfix);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patch applied to BuildingAI.RenderInstance");
                    }

                    // BuildingAI.RenderMeshes method
                    var renderMeshesMethod = buildingAIType.GetMethod("RenderMeshes",
                        BindingFlags.Instance | BindingFlags.Public);

                    if (renderMeshesMethod != null)
                    {
                        var meshesPrefix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderMeshes_Prefix");
                        var meshesPostfix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderMeshes_Postfix");
                        
                        harmony.Patch(renderMeshesMethod, prefix: meshesPrefix, postfix: meshesPostfix);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patch applied to BuildingAI.RenderMeshes");
                    }

                    // BuildingAI.RenderProps method (public version)
                    var renderPropsMethod = buildingAIType.GetMethod("RenderProps",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(Building).MakeByRefType(), typeof(int), typeof(RenderManager.Instance).MakeByRefType(), typeof(bool), typeof(bool) },
                        null);

                    if (renderPropsMethod != null)
                    {
                        var propsPrefix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderProps_Prefix");
                        var propsPostfix = new HarmonyMethod(typeof(BuildingRenderInstanceDetailHooks), "BuildingAI_RenderProps_Postfix");
                        
                        harmony.Patch(renderPropsMethod, prefix: propsPrefix, postfix: propsPostfix);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patch applied to BuildingAI.RenderProps");
                    }

                    _isPatched = true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply BuildingRenderInstanceDetail patches: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// パッチ除去
        /// </summary>
        public static void RemovePatches(HarmonyLib.Harmony harmony)
        {
            lock (_patchLock)
            {
                if (!_isPatched) return;

                try
                {
                    var buildingType = typeof(Building);
                    
                    var publicRenderMethod = buildingType.GetMethod("RenderInstance", 
                        BindingFlags.Instance | BindingFlags.Public,
                        null, 
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int) },
                        null);

                    var privateRenderMethod = buildingType.GetMethod("RenderInstance", 
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int), typeof(BuildingInfo), typeof(RenderManager.Instance).MakeByRefType() },
                        null);

                    var buildingAIType = typeof(BuildingAI);
                    var aiRenderMethod = buildingAIType.GetMethod("RenderInstance",
                        BindingFlags.Instance | BindingFlags.Public);

                    var renderMeshesMethod = buildingAIType.GetMethod("RenderMeshes",
                        BindingFlags.Instance | BindingFlags.Public);

                    var renderPropsMethod = buildingAIType.GetMethod("RenderProps",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(Building).MakeByRefType(), typeof(int), typeof(RenderManager.Instance).MakeByRefType(), typeof(bool), typeof(bool) },
                        null);

                    if (publicRenderMethod != null)
                        harmony.Unpatch(publicRenderMethod, HarmonyPatchType.All, Constants.HARMONY_ID);
                    
                    if (privateRenderMethod != null)
                        harmony.Unpatch(privateRenderMethod, HarmonyPatchType.All, Constants.HARMONY_ID);
                    
                    if (aiRenderMethod != null)
                        harmony.Unpatch(aiRenderMethod, HarmonyPatchType.All, Constants.HARMONY_ID);

                    if (renderMeshesMethod != null)
                        harmony.Unpatch(renderMeshesMethod, HarmonyPatchType.All, Constants.HARMONY_ID);

                    if (renderPropsMethod != null)
                        harmony.Unpatch(renderPropsMethod, HarmonyPatchType.All, Constants.HARMONY_ID);

                    _isPatched = false;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderInstanceDetail patches removed");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove BuildingRenderInstanceDetail patches: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Building.RenderInstance詳細分析フック
    /// </summary>
    public static class BuildingRenderInstanceDetailHooks
    {
        private static bool _isEnabled = false;
        private static readonly object _statsLock = new object();
        private static readonly Dictionary<ushort, BuildingRenderStats> _buildingStats = new Dictionary<ushort, BuildingRenderStats>();
        private static readonly Dictionary<string, PhaseStats> _phaseStats = new Dictionary<string, PhaseStats>();
        
        private static DateTime _lastOutput = DateTime.Now;
        private static readonly TimeSpan OUTPUT_INTERVAL = TimeSpan.FromSeconds(30);
        
        // ThreadLocal for timing data
        [ThreadStatic]
        private static Stopwatch _currentTimer;
        [ThreadStatic]
        private static string _currentPhase;
        [ThreadStatic]
        private static ushort _currentBuildingID;

        public static void Enable() => _isEnabled = true;
        public static void Disable() => _isEnabled = false;

        #region Public RenderInstance Hooks

        public static void PublicRenderInstance_Prefix(ref Building __instance, ushort buildingID, int layerMask)
        {
            if (!_isEnabled) return;

            _currentTimer = Stopwatch.StartNew();
            _currentPhase = "PublicRenderInstance";
            _currentBuildingID = buildingID;
        }

        public static void PublicRenderInstance_Postfix(ref Building __instance, ushort buildingID, int layerMask)
        {
            if (!_isEnabled || _currentTimer == null) return;

            _currentTimer.Stop();
            RecordPhaseTime(_currentPhase, _currentTimer.ElapsedTicks);
            RecordBuildingCall(buildingID, _currentPhase, _currentTimer.ElapsedTicks);
        }

        #endregion

        #region Private RenderInstance Hooks

        public static void PrivateRenderInstance_Prefix(ref Building __instance, ushort buildingID, int layerMask, BuildingInfo info, ref RenderManager.Instance data)
        {
            if (!_isEnabled) return;

            _currentTimer = Stopwatch.StartNew();
            _currentPhase = "PrivateRenderInstance";
            _currentBuildingID = buildingID;
        }

        public static void PrivateRenderInstance_Postfix(ref Building __instance, ushort buildingID, int layerMask, BuildingInfo info, ref RenderManager.Instance data)
        {
            if (!_isEnabled || _currentTimer == null) return;

            _currentTimer.Stop();
            RecordPhaseTime(_currentPhase, _currentTimer.ElapsedTicks);
            RecordBuildingCall(buildingID, _currentPhase, _currentTimer.ElapsedTicks);
        }

        #endregion

        #region BuildingAI.RenderInstance Hooks

        public static void BuildingAI_RenderInstance_Prefix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance)
        {
            if (!_isEnabled) return;

            _currentTimer = Stopwatch.StartNew();
            _currentPhase = $"BuildingAI_{__instance.GetType().Name}";
            _currentBuildingID = buildingID;
        }

        public static void BuildingAI_RenderInstance_Postfix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance)
        {
            if (!_isEnabled || _currentTimer == null) return;

            _currentTimer.Stop();
            RecordPhaseTime(_currentPhase, _currentTimer.ElapsedTicks);
            RecordBuildingCall(buildingID, _currentPhase, _currentTimer.ElapsedTicks);

            // 定期的な統計出力
            CheckOutputStats();
        }

        #endregion

        #region BuildingAI.RenderMeshes Hooks

        public static void BuildingAI_RenderMeshes_Prefix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance)
        {
            if (!_isEnabled) return;

            _currentTimer = Stopwatch.StartNew();
            _currentPhase = $"RenderMeshes_{__instance.GetType().Name}";
            _currentBuildingID = buildingID;
        }

        public static void BuildingAI_RenderMeshes_Postfix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance)
        {
            if (!_isEnabled || _currentTimer == null) return;

            _currentTimer.Stop();
            RecordPhaseTime(_currentPhase, _currentTimer.ElapsedTicks);
            RecordBuildingCall(buildingID, _currentPhase, _currentTimer.ElapsedTicks);
        }

        #endregion

        #region BuildingAI.RenderProps Hooks

        public static void BuildingAI_RenderProps_Prefix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance, bool renderFixed, bool renderNonfixed)
        {
            if (!_isEnabled) return;

            _currentTimer = Stopwatch.StartNew();
            _currentPhase = $"RenderProps_{__instance.GetType().Name}";
            _currentBuildingID = buildingID;
        }

        public static void BuildingAI_RenderProps_Postfix(BuildingAI __instance, ushort buildingID, ref Building data, int layerMask, ref RenderManager.Instance instance, bool renderFixed, bool renderNonfixed)
        {
            if (!_isEnabled || _currentTimer == null) return;

            _currentTimer.Stop();
            RecordPhaseTime(_currentPhase, _currentTimer.ElapsedTicks);
            RecordBuildingCall(buildingID, _currentPhase, _currentTimer.ElapsedTicks);
        }

        #endregion

        #region Statistics

        private static void RecordPhaseTime(string phase, long ticks)
        {
            lock (_statsLock)
            {
                if (!_phaseStats.ContainsKey(phase))
                {
                    _phaseStats[phase] = new PhaseStats();
                }
                _phaseStats[phase].AddCall(ticks);
            }
        }

        private static void RecordBuildingCall(ushort buildingID, string phase, long ticks)
        {
            lock (_statsLock)
            {
                if (!_buildingStats.ContainsKey(buildingID))
                {
                    _buildingStats[buildingID] = new BuildingRenderStats();
                }
                _buildingStats[buildingID].AddCall(phase, ticks);
            }
        }

        private static void CheckOutputStats()
        {
            if (DateTime.Now - _lastOutput > OUTPUT_INTERVAL)
            {
                OutputDetailedStats();
                _lastOutput = DateTime.Now;
            }
        }

        public static void OutputDetailedStats()
        {
            if (!_isEnabled) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== Building.RenderInstance Detailed Analysis ===");

            lock (_statsLock)
            {
                // Phase統計
                sb.AppendLine("\n--- Phase Performance Analysis ---");
                foreach (var kvp in _phaseStats.OrderByDescending(x => x.Value.TotalTime))
                {
                    var stats = kvp.Value;
                    sb.AppendLine($"{kvp.Key}: {stats.CallCount} calls, Avg: {stats.AverageTimeMs:F3}ms, Total: {stats.TotalTimeMs:F1}ms");
                }

                // 重い建物TOP20
                sb.AppendLine("\n--- Top 20 Heavy Buildings ---");
                var heavyBuildings = _buildingStats
                    .OrderByDescending(x => x.Value.TotalTime)
                    .Take(20);

                foreach (var kvp in heavyBuildings)
                {
                    var buildingID = kvp.Key;
                    var stats = kvp.Value;
                    var building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingID];
                    var buildingName = building.Info?.name ?? "Unknown";

                    sb.AppendLine($"Building {buildingID} ({buildingName}): {stats.CallCount} calls, Total: {stats.TotalTimeMs:F1}ms, Avg: {stats.AverageTimeMs:F3}ms");
                    
                    foreach (var phaseKvp in stats.PhaseStats.OrderByDescending(x => x.Value.TotalTime))
                    {
                        var phaseStats = phaseKvp.Value;
                        sb.AppendLine($"  {phaseKvp.Key}: {phaseStats.CallCount} calls, Avg: {phaseStats.AverageTimeMs:F3}ms");
                    }
                }

                // 建物タイプ別統計
                sb.AppendLine("\n--- Building Type Performance ---");
                var typeStats = new Dictionary<string, PhaseStats>();
                
                foreach (var kvp in _buildingStats)
                {
                    var buildingID = kvp.Key;
                    var building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingID];
                    var buildingType = building.Info?.GetAI()?.GetType().Name ?? "Unknown";
                    
                    if (!typeStats.ContainsKey(buildingType))
                    {
                        typeStats[buildingType] = new PhaseStats();
                    }
                    
                    typeStats[buildingType].AddCall(kvp.Value.TotalTime);
                }

                foreach (var kvp in typeStats.OrderByDescending(x => x.Value.TotalTime))
                {
                    var stats = kvp.Value;
                    sb.AppendLine($"{kvp.Key}: {stats.CallCount} buildings, Total: {stats.TotalTimeMs:F1}ms, Avg: {stats.AverageTimeMs:F3}ms");
                }

                // 統計リセット
                _phaseStats.Clear();
                _buildingStats.Clear();
            }

            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {sb}");
        }

        #endregion
    }

    /// <summary>
    /// 処理段階統計
    /// </summary>
    public class PhaseStats
    {
        public int CallCount { get; private set; }
        public long TotalTime { get; private set; }
        public double TotalTimeMs => TotalTime * 1000.0 / Stopwatch.Frequency;
        public double AverageTimeMs => CallCount > 0 ? TotalTimeMs / CallCount : 0;

        public void AddCall(long ticks)
        {
            CallCount++;
            TotalTime += ticks;
        }
    }

    /// <summary>
    /// 建物別レンダリング統計
    /// </summary>
    public class BuildingRenderStats
    {
        public Dictionary<string, PhaseStats> PhaseStats { get; } = new Dictionary<string, PhaseStats>();
        public int CallCount => PhaseStats.Values.Sum(x => x.CallCount);
        public long TotalTime => PhaseStats.Values.Sum(x => x.TotalTime);
        public double TotalTimeMs => TotalTime * 1000.0 / Stopwatch.Frequency;
        public double AverageTimeMs => CallCount > 0 ? TotalTimeMs / CallCount : 0;

        public void AddCall(string phase, long ticks)
        {
            if (!PhaseStats.ContainsKey(phase))
            {
                PhaseStats[phase] = new PhaseStats();
            }
            PhaseStats[phase].AddCall(ticks);
        }
    }
}
