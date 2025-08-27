using ColossalFramework;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// CS1Profiler の統合Harmonyパッチシステム
    /// </summary>
    public static class Patcher
    {
        private const string HarmonyId = "me.cs1profiler.startup";
        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;

            patched = true;
            var harmony = new HarmonyLib.Harmony(HarmonyId);

            // SimulationManagerパッチ（既存の重要な処理）
            try
            {
                ApplySimulationManagerPatch(harmony);
                UnityEngine.Debug.Log("[CS1Profiler] Basic patches applied successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Basic patches failed: " + e.Message);
            }

            // パフォーマンス測定パッチ
            try
            {
                PerformancePatches.ApplyPerformancePatches(harmony);
                UnityEngine.Debug.Log("[CS1Profiler] Performance patches applied successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Performance patches failed: " + e.Message);
            }

            // 起動時解析パッチ
            try
            {
                StartupPatches.PatchStartupMethods(harmony);
                UnityEngine.Debug.Log("[CS1Profiler] Startup patches applied successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Startup patches failed: " + e.Message);
            }

            // RenderIt最適化パッチ
            try
            {
                RenderItOptimization.ApplyRenderItOptimizationPatches(harmony);
                UnityEngine.Debug.Log("[CS1Profiler] RenderIt optimization patches applied successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] RenderIt optimization patches failed: " + e.Message);
            }
        }

        private static void ApplySimulationManagerPatch(HarmonyLib.Harmony harmony)
        {
            var simType = typeof(SimulationManager);
            var simMethod = simType.GetMethod("SimulationStepImpl", 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (simMethod != null)
            {
                harmony.Patch(simMethod, 
                    prefix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod("Pre")),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod("Post")));
                UnityEngine.Debug.Log("[CS1Profiler] SimulationManager.SimulationStepImpl patched");
            }
        }

        public static void UnpatchAll()
        {
            if (!patched) return;
            
            try
            {
                var harmony = new HarmonyLib.Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
                patched = false;
                UnityEngine.Debug.Log("[CS1Profiler] All patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] UnpatchAll failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// SimulationManager用の専用フック
    /// </summary>
    internal static class Hooks
    {
        public static void Pre(System.Reflection.MethodBase __originalMethod)
        {
            CS1Profiler.Profiling.PerformanceProfiler.MethodStart(__originalMethod);
        }

        public static void Post(System.Reflection.MethodBase __originalMethod)
        {
            CS1Profiler.Profiling.PerformanceProfiler.MethodEnd();
        }
    }
}
