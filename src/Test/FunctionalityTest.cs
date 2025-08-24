using UnityEngine;
using System;

namespace CS1Profiler.Test
{
    /// <summary>
    /// 基本機能の動作テスト用クラス
    /// </summary>
    public class FunctionalityTest : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[CS1Profiler] === FUNCTIONALITY TEST ===");
            
            // 1. ProfilerManager確認
            TestProfilerManager();
            
            // 2. CSV機能確認
            TestCSVFunction();
            
            // 3. Harmonyパッチ確認
            TestHarmonyPatches();
        }

        void TestProfilerManager()
        {
            try
            {
                if (CS1Profiler.Managers.ProfilerManager.Instance != null)
                {
                    Debug.Log("[TEST] ✅ ProfilerManager.Instance is available");
                    bool isEnabled = CS1Profiler.Managers.ProfilerManager.Instance.IsProfilingEnabled();
                    Debug.Log($"[TEST] Profiling enabled: {isEnabled}");
                }
                else
                {
                    Debug.LogError("[TEST] ❌ ProfilerManager.Instance is NULL");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TEST] ❌ ProfilerManager test failed: {e.Message}");
            }
        }

        void TestCSVFunction()
        {
            try
            {
                if (CS1Profiler.Managers.ProfilerManager.Instance != null)
                {
                    string csvPath = CS1Profiler.Managers.ProfilerManager.Instance.GetCsvPath();
                    Debug.Log($"[TEST] CSV Path: {csvPath}");
                    
                    // CSV書き込みテスト
                    CS1Profiler.Managers.ProfilerManager.Instance.ExportToCSV();
                    Debug.Log("[TEST] ✅ CSV export test completed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TEST] ❌ CSV test failed: {e.Message}");
            }
        }

        void TestHarmonyPatches()
        {
            try
            {
                // ログ抑制機能テスト
                bool currentState = CS1Profiler.Harmony.LogSuppressionHooks.SuppressPackageDeserializerLogs;
                Debug.Log($"[TEST] LogSuppression current state: {currentState}");
                
                // 状態を変更してテスト
                CS1Profiler.Harmony.LogSuppressionHooks.SuppressPackageDeserializerLogs = !currentState;
                Debug.Log($"[TEST] LogSuppression toggled to: {!currentState}");
                
                // 元に戻す
                CS1Profiler.Harmony.LogSuppressionHooks.SuppressPackageDeserializerLogs = currentState;
                Debug.Log("[TEST] ✅ LogSuppression test completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TEST] ❌ Harmony test failed: {e.Message}");
            }
        }
    }
}
