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

        public static void PatchAll()
        {
            if (patched) return;

            patched = true;
            var harmony = new HarmonyLib.Harmony(HarmonyId);

            // 要件対応: Manager/AI/Controller系クラスをターゲットにパッチング
            ApplyPerformancePatches(harmony);

            // --- PackageDeserializerログ抑制パッチ ---
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying log suppression patches...");
                LogSuppressionPatcher.ApplyPatches(harmony);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Log suppression patches failed: " + e.Message);
            }

            // --- 起動時解析用のパッチを追加 ---
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying startup analysis patches...");
                StartupAnalysisPatcher.ApplyPatches(harmony);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Startup patches failed: " + e.Message);
            }

            // --- 既存のSimulationStepImplパッチ ---
            try
            {
                SimulationPatcher.ApplyPatches(harmony);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] SimulationManager patch failed: " + e.Message);
            }

            UnityEngine.Debug.Log("[CS1Profiler] All Harmony patches applied successfully.");
            
            // PatchControllerの初期状態をログ出力
            PatchController.LogInitialState();
        }

        // パフォーマンス測定用のブラックリストシステム
        private static readonly HashSet<string> _modAssemblyNames = new HashSet<string>();
        private static readonly HashSet<string> _modTypeNames = new HashSet<string>();
        
        // 要件対応: Manager/AI/Controller系クラスの性能測定パッチ（統一されたブラックリスト制）
        private static void ApplyPerformancePatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying performance measurement patches (unified blacklist system)...");
                
                // MOD検出
                DetectRealModAssemblies();
                
                // 統一されたパッチシステム
                PerformancePatcher.ApplyBlacklistPatches(harmony, _modAssemblyNames, _modTypeNames);
                
                UnityEngine.Debug.Log("[CS1Profiler] Performance measurement patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ApplyPerformancePatches failed: " + e.Message);
            }
        }
        
        private static void DetectRealModAssemblies()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Detecting MOD assemblies...");
                
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
                
                UnityEngine.Debug.Log($"[CS1Profiler] Detected {_modAssemblyNames.Count} MOD assemblies, {_modTypeNames.Count} critical types");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] DetectRealModAssemblies error: {e.Message}");
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

            var harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);
            patched = false;
        }
    }
}
