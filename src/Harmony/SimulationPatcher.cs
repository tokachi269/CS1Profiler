using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// SimulationManager専用のHarmonyパッチ管理
    /// SimulationStepとSimulationStepImplの実行時間測定
    /// </summary>
    public static class SimulationPatcher
    {
        private static List<MethodInfo> patchedMethods = new List<MethodInfo>();

        /// <summary>
        /// シミュレーション測定パッチを完全に削除
        /// </summary>
        public static void RemovePatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Removing simulation measurement patches...");
                
                int removedCount = 0;
                foreach (var method in patchedMethods)
                {
                    try
                    {
                        // LegacySimulationHooksのパッチを削除
                        harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Pre"));
                        harmony.Unpatch(method, typeof(LegacySimulationHooks).GetMethod("Post"));
                        
                        // LightweightPerformanceHooksのパッチも削除
                        harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPrefix"));
                        harmony.Unpatch(method, typeof(CS1Profiler.Profiling.LightweightPerformanceHooks).GetMethod("ProfilerPostfix"));
                        
                        removedCount++;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to remove simulation patch from {method.DeclaringType?.Name}.{method.Name}: {e.Message}");
                    }
                }
                
                patchedMethods.Clear();
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Removed {removedCount} simulation patches");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} RemoveSimulationPatches error: {e.Message}");
            }
        }
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                // SimulationManagerのみに限定
                var simType = typeof(SimulationManager);
                var simMethod = simType.GetMethod("SimulationStepImpl", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (simMethod != null && simMethod.GetMethodBody() != null)
                {
                    harmony.Patch(
                        original: simMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(LegacySimulationHooks).GetMethod("Pre")),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(LegacySimulationHooks).GetMethod("Post"))
                    );
                    patchedMethods.Add(simMethod); // パッチしたメソッドを記録
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} SimulationManager.SimulationStepImpl patched successfully (legacy system)");
                }

                // さらにSimulationManager.SimulationStepもパフォーマンス測定対象に
                var simStepMethod = simType.GetMethod("SimulationStep", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (simStepMethod != null)
                {
                    var prefix = new HarmonyLib.HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPrefix");
                    var postfix = new HarmonyLib.HarmonyMethod(typeof(CS1Profiler.Profiling.LightweightPerformanceHooks), "ProfilerPostfix");
                    harmony.Patch(simStepMethod, prefix, postfix);
                    patchedMethods.Add(simStepMethod); // パッチしたメソッドを記録
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} SimulationManager.SimulationStep patched (new system)");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} SimulationPatcher failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// レガシーなSimulationStepImpl用のフッククラス
    /// 後方互換性のため維持、PatchControllerによる統一制御を使用
    /// </summary>
    public static class LegacySimulationHooks
    {
        private static int _id;
        private static long _tsc;
        
        private static readonly int MaxMethods = 50; // 安全な値を維持
        private static readonly int SampleEveryNFrames = 60; // 負荷軽減
        private static Dictionary<MethodBase, int> _methodToId;
        private static Dictionary<int, MethodBase> _byId;
        private static long[] _ns;
        private static int[] _cnt;
        private static int _nextId = 0;

        // 静的コンストラクタを削除し、必要時に初期化するように変更
        private static void EnsureInitialized()
        {
            try 
            {
                // 基本的なフィールドを初期化
                if (_methodToId == null) _methodToId = new Dictionary<MethodBase, int>();
                if (_byId == null) _byId = new Dictionary<int, MethodBase>();
                if (_ns == null) _ns = new long[MaxMethods];
                if (_cnt == null) _cnt = new int[MaxMethods];
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} EnsureInitialized failed: " + e.Message);
            }
        }

        public static void ToggleProfiling()
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === SimulationProfiling ToggleProfiling() CALLED ===");
            
            bool currentState = PatchController.SimulationProfilingEnabled;
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Current SimulationProfiling state: " + currentState);
            
            PatchController.SimulationProfilingEnabled = !currentState;
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} New SimulationProfiling state: " + PatchController.SimulationProfilingEnabled);
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} SimulationProfiling: " + (PatchController.SimulationProfilingEnabled ? "ON" : "OFF"));
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === SimulationProfiling ToggleProfiling() COMPLETED ===");
        }
        
        public static bool IsEnabled()
        {
            return PatchController.SimulationProfilingEnabled;
        }

        private static int GetId(MethodBase method)
        {
            try
            {
                if (_methodToId == null || _byId == null) return 0;
                
                if (!_methodToId.TryGetValue(method, out int id))
                {
                    id = _nextId++;
                    if (id >= MaxMethods) return 0;
                    _methodToId[method] = id;
                    _byId[id] = method;
                }
                return id;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} GetId error: " + e.Message);
                return 0;
            }
        }

        private static bool ShouldSample()
        {
            if (SampleEveryNFrames <= 1) return true;
            return (UnityEngine.Time.frameCount % SampleEveryNFrames) == 0;
        }

        public static void Pre(MethodBase __originalMethod)
        {
            if (!PatchController.SimulationProfilingEnabled || !ShouldSample()) return;
            EnsureInitialized();
            _id  = GetId(__originalMethod);
            _tsc = Stopwatch.GetTimestamp();
        }

        public static void Post()
        {
            if (!PatchController.SimulationProfilingEnabled || !ShouldSample()) return;
            
            try
            {
                long elapsed = Stopwatch.GetTimestamp() - _tsc;
                if (_id > 0 && _id < MaxMethods && _ns != null && _cnt != null)
                {
                    _ns[_id] += elapsed * 1000000000L / Stopwatch.Frequency;
                    _cnt[_id]++;
                }
                
                // 統計出力頻度を大幅に下げる（10分間隔）
                if (UnityEngine.Time.frameCount % 36000 == 0)
                {
                    DumpTop(10);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Post method error: " + e.Message);
            }
        }

        private static void DumpTop(int n)
        {
            try
            {
                if (_ns == null || _cnt == null || _byId == null) return;
                
                var list = new List<ProfileEntry>();
                for (int i = 1; i < _nextId && i < MaxMethods; i++)
                {
                    if (_cnt[i] > 0) list.Add(new ProfileEntry { id = i, ns = _ns[i], cnt = _cnt[i] });
                }
                
                // ラムダ式を避けるため、手動でソート
                for (int i = 0; i < list.Count - 1; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (list[i].ns < list[j].ns)
                        {
                            var temp = list[i];
                            list[i] = list[j];
                            list[j] = temp;
                        }
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{Constants.LOG_PREFIX} Top" + n + " (frame " + UnityEngine.Time.frameCount + ") WITH CSV");
                
                for (int i = 0; i < Math.Min(n, list.Count); i++)
                {
                    var entry = list[i];
                    double ms = entry.ns / 1000000.0;
                    double avgUs = (ms * 1000.0) / entry.cnt;
                    var m = _byId[entry.id];
                    string typeName = m.DeclaringType != null ? m.DeclaringType.Name : "Unknown";
                    string methodName = m.Name;
                    
                    sb.Append(i+1).Append(". ")
                      .Append(typeName).Append(".").Append(methodName)
                      .Append("  total:").Append(ms.ToString("F3")).Append("ms")
                      .Append("  calls:").Append(entry.cnt)
                      .Append("  avg:").Append(avgUs.ToString("F2")).Append("us")
                      .AppendLine();
                    
                    // 各メソッドの統計をCSVキューに追加（ProfilerManager経由）
                    if (CS1Profiler.Managers.ProfilerManager.Instance != null && CS1Profiler.Managers.ProfilerManager.Instance.CsvManager != null)
                    {
                        CS1Profiler.Managers.ProfilerManager.Instance.CsvManager.QueueCsvWrite(typeName, methodName, ms, entry.cnt, avgUs, i + 1);
                    }
                    
                    _ns[entry.id] = 0; _cnt[entry.id] = 0;
                }
                
                UnityEngine.Debug.Log(sb.ToString());
                
                // システム統計もCSVに記録（ProfilerManager経由）
                if (CS1Profiler.Managers.ProfilerManager.Instance != null && CS1Profiler.Managers.ProfilerManager.Instance.CsvManager != null)
                {
                    float currentFps = 1.0f / UnityEngine.Time.deltaTime;
                    long memMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    CS1Profiler.Managers.ProfilerManager.Instance.CsvManager.QueueCsvWrite("System", "FrameStats", currentFps, 1, 0, 0, "FPS=" + currentFps.ToString("F1") + ",Memory=" + memMB + "MB");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} DumpTop error: " + e.Message);
            }
        }

        private struct ProfileEntry
        {
            public int id;
            public long ns;
            public int cnt;
        }
    }
}
