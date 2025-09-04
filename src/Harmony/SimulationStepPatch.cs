using System;
using System.Diagnostics;
using HarmonyLib;
using System.Reflection;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// SimulationManagerのSimulationStepメソッド用のHarmonyパッチ
    /// </summary>
    [HarmonyPatch]
    public static class SimulationStepPatch
    {
        private static Stopwatch _stopwatch = new Stopwatch();
        private static MethodBase _currentMethod;
        
        /// <summary>
        /// SimulationManagerのSimulationStepメソッドを対象とするパッチ
        /// </summary>
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            try
            {
                var simulationManagerType = typeof(SimulationManager);
                
                // SimulationStepメソッドを検索
                var methods = simulationManagerType.GetMethods(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                foreach (var method in methods)
                {
                    if (method.Name == "SimulationStep" || method.Name == "SimulationStepImpl")
                    {
                        UnityEngine.Debug.Log($"[CS1Profiler] Found target method: {method.Name}");
                        return method;
                    }
                }
                
                UnityEngine.Debug.LogError("[CS1Profiler] SimulationStep method not found");
                return null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] TargetMethod error: {e.Message}");
                return null;
            }
        }
        
        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod)
        {
            try
            {
                _currentMethod = __originalMethod;
                _stopwatch.Stop();
                _stopwatch.Reset();
                _stopwatch.Start();
                
                // MethodProfilerにメソッド開始を通知
                CS1Profiler.Profiling.MethodProfiler.MethodStart(__originalMethod);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] SimulationStepPatch.Prefix error: {e.Message}");
            }
        }
        
        [HarmonyPostfix]
        public static void Postfix(MethodBase __originalMethod)
        {
            try
            {
                _stopwatch.Stop();
                
                // MethodProfilerにメソッド終了を通知
                CS1Profiler.Profiling.MethodProfiler.MethodEnd(__originalMethod);
                
                // 実行時間をログ出力（デバッグ用）
                var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
                if (elapsedMs > 50.0) // 50ms以上の場合のみログ
                {
                    UnityEngine.Debug.Log($"[CS1Profiler] SimulationStep: {elapsedMs:F2}ms");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] SimulationStepPatch.Postfix error: {e.Message}");
            }
        }
    }
}
