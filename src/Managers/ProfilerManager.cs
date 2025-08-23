// Cities: Skylines (CS1) 用：プロファイラー管理クラス

using System;
using CitiesHarmony.API;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// ProfilerManager - メインのプロファイラー処理を管理
    /// </summary>
    public class ProfilerManager : MonoBehaviour
    {
        public static ProfilerManager Instance;
        
        // Harmonyとパッチ関連
        private bool _isInitialized = false;
        
        // CSV出力管理
        public CSVManager CsvManager { get; private set; }
        
        public void Initialize()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager.Initialize() called");
                
                if (_isInitialized)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager already initialized");
                    return;
                }
                
                // CSV管理システム初期化
                CsvManager = new CSVManager();
                CsvManager.Initialize();
                
                // Harmony初期化
                InitializeHarmony();
                
                _isInitialized = true;
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager initialization completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerManager.Initialize() error: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Stack trace: " + e.StackTrace);
            }
        }
        
        private void InitializeHarmony()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Initializing Harmony patches...");
                Mod.LogStartupEvent("HARMONY_INIT_START", "Starting Harmony initialization");
                HarmonyHelper.DoOnHarmonyReady(InitializePatcher);
                Mod.LogStartupEvent("HARMONY_INIT_SUCCESS", "Harmony initialization completed");
                UnityEngine.Debug.Log("[CS1Profiler] Harmony patches applied successfully");
            }
            catch (Exception e)
            {
                Mod.LogStartupEvent("HARMONY_INIT_ERROR", "Harmony initialization failed: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Harmony initialization failed: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Harmony stack trace: " + e.StackTrace);
            }
        }
        
        private static void InitializePatcher()
        {
            UnityEngine.Debug.Log("[CS1Profiler] InitializePatcher called");
            Patcher.PatchAll();
        }
        
        public void ToggleProfiling()
        {
            try
            {
                Hooks.ToggleProfiling();
                string status = Hooks.IsEnabled() ? "STARTED" : "STOPPED";
                UnityEngine.Debug.Log("[CS1Profiler] Profiling " + status + " via ProfilerManager");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ToggleProfiling error: " + e.Message);
            }
        }
        
        public bool IsProfilingEnabled()
        {
            return Hooks.IsEnabled();
        }
        
        public string GetCsvPath()
        {
            return CsvManager != null ? CsvManager.GetCsvFilePath() : "CSV Manager not initialized";
        }
        
        void OnDestroy()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager.OnDestroy() called");
                
                // CSV管理システムクリーンアップ
                if (CsvManager != null)
                {
                    CsvManager.Cleanup();
                    CsvManager = null;
                }
                
                // Harmonyパッチクリーンアップ
                try
                {
                    if (HarmonyHelper.IsHarmonyInstalled)
                    {
                        Patcher.UnpatchAll();
                    }
                }
                catch (Exception harmonyError)
                {
                    UnityEngine.Debug.LogError("[CS1Profiler] Harmony cleanup error: " + harmonyError.Message);
                }
                
                _isInitialized = false;
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager cleanup completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerManager.OnDestroy() error: " + e.Message);
            }
        }
    }
}
