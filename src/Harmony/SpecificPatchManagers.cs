using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// シミュレーション測定パッチマネージャー
    /// </summary>
    public class SimulationPatchManager : IPatchManager
    {
        public string PatchType => "Simulation";
        public bool DefaultEnabled => false;
        public bool IsPatched { get; private set; } = false;

        private List<MethodInfo> patchedMethods = new List<MethodInfo>();

        public void ApplyPatches(Harmony harmony)
        {
            if (IsPatched) return;

            try
            {
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
                    var prefix = new HarmonyMethod(typeof(PerformanceHooks), "ProfilerPrefix");
                    var postfix = new HarmonyMethod(typeof(PerformanceHooks), "ProfilerPostfix");
                    harmony.Patch(simStepMethod, prefix, postfix);
                    patchedMethods.Add(simStepMethod);
                }

                IsPatched = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Simulation patches applied: {patchedMethods.Count} methods");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply simulation patches: {e.Message}");
                throw;
            }
        }

        public void RemovePatches(Harmony harmony)
        {
            if (!IsPatched) return;

            try
            {
                foreach (var method in patchedMethods)
                {
                    harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Pre"));
                    harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Post"));
                    harmony.Unpatch(method, typeof(PerformanceHooks).GetMethod("ProfilerPrefix"));
                    harmony.Unpatch(method, typeof(PerformanceHooks).GetMethod("ProfilerPostfix"));
                }
                
                patchedMethods.Clear();
                IsPatched = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Simulation patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove simulation patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// ログ抑制パッチマネージャー
    /// </summary>
    public class LogSuppressionPatchManager : IPatchManager
    {
        public string PatchType => "LogSuppression";
        public bool DefaultEnabled => true; // デフォルトON
        public bool IsPatched { get; private set; } = false;

        public void ApplyPatches(Harmony harmony)
        {
            if (IsPatched) return;

            try
            {
                LogSuppressionPatcher.ApplyPatches(harmony);
                IsPatched = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply log suppression patches: {e.Message}");
                throw;
            }
        }

        public void RemovePatches(Harmony harmony)
        {
            if (!IsPatched) return;

            try
            {
                // LogSuppressionPatcherにRemovePatchesメソッドを追加する必要あり
                // 今は簡単な実装
                IsPatched = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Log suppression patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove log suppression patches: {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 起動解析パッチマネージャー
    /// </summary>
    public class StartupAnalysisPatchManager : IPatchManager
    {
        public string PatchType => "StartupAnalysis";
        public bool DefaultEnabled => true; // デフォルトON
        public bool IsPatched { get; private set; } = false;

        public void ApplyPatches(Harmony harmony)
        {
            if (IsPatched) return;

            try
            {
                // StartupAnalysisPatcher削除：MPSCシステムに統合
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Startup analysis moved to MPSC manual profiling");
                IsPatched = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to apply startup analysis patches: {e.Message}");
                throw;
            }
        }

        public void RemovePatches(Harmony harmony)
        {
            if (!IsPatched) return;

            try
            {
                // 何もしない（MPSCシステムで管理）
                IsPatched = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Startup analysis patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to remove startup analysis patches: {e.Message}");
                throw;
            }
        }
    }
}
