using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using UnityEngine;
using CS1Profiler.Managers;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// Harmonyパッチの統括管理クラス
    /// 全パッチの適用・削除を一元管理
    /// </summary>
    public static class MainPatcher
    {
        private const string HarmonyId = "me.cs1profiler.startup";
        private static bool patched = false;
        private static HarmonyLib.Harmony harmonyInstance = null;

        /// <summary>
        /// パフォーマンス測定パッチの動的適用
        /// </summary>
        public static void EnablePerformancePatches()
        {
            if (harmonyInstance == null || !patched) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling performance patches...");
                ApplyPerformancePatches(harmonyInstance);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable performance patches: " + e.Message);
            }
        }

        /// <summary>
        /// パフォーマンス測定パッチの動的削除
        /// </summary>
        public static void DisablePerformancePatches()
        {
            if (harmonyInstance == null) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling performance patches...");
                PerformancePatcher.RemovePatches(harmonyInstance);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable performance patches: " + e.Message);
            }
        }

        /// <summary>
        /// シミュレーション測定パッチの動的適用
        /// </summary>
        public static void EnableSimulationPatches()
        {
            if (harmonyInstance == null || !patched) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling simulation patches...");
                SimulationPatcher.ApplyPatches(harmonyInstance);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable simulation patches: " + e.Message);
            }
        }

        /// <summary>
        /// シミュレーション測定パッチの動的削除
        /// </summary>
        public static void DisableSimulationPatches()
        {
            if (harmonyInstance == null) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling simulation patches...");
                SimulationPatcher.RemovePatches(harmonyInstance);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable simulation patches: " + e.Message);
            }
        }

        public static void PatchAll()
        {
            if (patched) return;

            patched = true;
            harmonyInstance = new HarmonyLib.Harmony(HarmonyId);

            // デフォルトではパフォーマンス測定系パッチは適用しない（性能重視）
            // PatchController.PerformanceProfilingEnabledがtrueの場合のみ適用

            // --- PackageDeserializerログ抑制パッチ ---
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Applying log suppression patches...");
                LogSuppressionPatcher.ApplyPatches(harmonyInstance);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Log suppression patches failed: " + e.Message);
            }

            // --- 起動時解析は手動開始に変更（StartupAnalysisPatcher削除） ---
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Startup analysis moved to manual activation via UI button.");

            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Essential patches applied. Performance measurement patches disabled by default.");
            
            // PatchControllerの初期状態をログ出力
            var status = PatchController.GetStatusString();
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Initial Patch Status: {status}");
        }

        // パフォーマンス測定用のブラックリストシステム
        private static readonly HashSet<string> _modAssemblyNames = new HashSet<string>();
        private static readonly HashSet<string> _modTypeNames = new HashSet<string>();
        
        // 要件対応: Manager/AI/Controller系クラスの性能測定パッチ（統一されたブラックリスト制）
        private static void ApplyPerformancePatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Applying performance measurement patches (unified blacklist system)...");
                
                // MOD検出
                DetectRealModAssemblies();
                
                // 統一されたパッチシステム
                PerformancePatcher.ApplyBlacklistPatches(harmony, _modAssemblyNames, _modTypeNames);
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance measurement patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} ApplyPerformancePatches failed: " + e.Message);
            }
        }
        
        private static void DetectRealModAssemblies()
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Detecting MOD assemblies...");
                
                var pluginManager = PluginManager.instance;
                if (pluginManager?.GetPluginsInfo() == null) return;
                
                foreach (var plugin in pluginManager.GetPluginsInfo())
                {
                    if (!plugin.isEnabled || plugin.isBuiltin) continue;
                    
                    foreach (var assembly in plugin.GetAssemblies())
                    {
                        if (assembly != null)
                        {
                            _modAssemblyNames.Add(assembly.GetName().Name);
                            
                            // すべてのタイプを検出（ブラックリスト制）
                            try
                            {
                                var types = assembly.GetTypes();
                                int addedTypes = 0;
                                
                                foreach (var type in types)
                                {
                                    // ブラックリスト：除外すべきタイプ
                                    if (IsBlacklistedType(type)) continue;
                                    
                                    // それ以外はすべて重要なタイプとして追加
                                    _modTypeNames.Add(type.FullName);
                                    addedTypes++;
                                }
                            }
                            catch { /* 一部のアセンブリは型を取得できない */ }
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Detected {_modAssemblyNames.Count} MOD assemblies, {_modTypeNames.Count} critical types");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} DetectRealModAssemblies error: {e.Message}");
            }
        }

        private static bool IsBlacklistedType(Type type)
        {
            return type.IsAbstract || type.IsInterface || type.IsEnum ||
                   type.Namespace?.StartsWith("System") == true ||
                   type.Namespace?.StartsWith("Microsoft") == true ||
                   type.FullName?.Contains("UnityEngine") == true ||
                   type.Name.Contains("Exception") ||
                   type.Name.Contains("Debug") ||
                   type.Name.Contains("Console") ||
                   // 不要なMOD除外
                   type.FullName?.StartsWith("CSShared") == true ||
                   type.FullName?.Contains(".IO.") == true ||
                   type.FullName?.StartsWith("ModTools") == true ||
                   type.FullName?.StartsWith("ExtendedAssetEditor") == true ||
                   type.FullName?.StartsWith("CS1Profiler") == true ||
                   type.FullName?.StartsWith("JsonFx") == true ||
                   // クラッシュ原因MOD除外
                   type.FullName?.StartsWith("LineToolMod") == true ||
                   // Harmony関連MOD除外
                   type.FullName?.Contains("PatchAll") == true ||
                   type.FullName?.Contains("Harmony") == true ||
                   // Keybind関連MOD除外
                   type.FullName?.Contains("Keybind") == true ||
                   type.FullName?.Contains("KeyBinding") == true ||
                   type.FullName?.Contains("Shortcut") == true;
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Removing all Harmony patches...");
                if (harmonyInstance != null)
                {
                    harmonyInstance.UnpatchAll(HarmonyId);
                }
                patched = false;
                harmonyInstance = null;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} All patches removed successfully.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove patches: " + e.Message);
            }
        }
    }
}
