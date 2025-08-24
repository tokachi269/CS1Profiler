using System;
using UnityEngine;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// プロファイラーの全体管理（MonoBehaviour対応）
    /// </summary>
    public class ProfilerManager : MonoBehaviour
    {
        private static ProfilerManager instance;
        public static ProfilerManager Instance => instance;
        
        private CSVManager csvManager;
        private bool isProfilingEnabled = true;
        private bool isInitialized = false;
        private HarmonyLib.Harmony harmonyInstance;
        private int csvExportInterval = 60;
        
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            try
            {
                if (isInitialized) return;

                Debug.Log("[CS1Profiler] ProfilingManager initializing...");
                
                csvManager = new CSVManager();
                csvManager.Initialize();

                if (CitiesHarmony.API.HarmonyHelper.IsHarmonyInstalled)
                {
                    harmonyInstance = new HarmonyLib.Harmony("CS1Profiler.MethodProfiler");
                    CS1Profiler.Profiling.MethodProfiler.Initialize(harmonyInstance);
                    isProfilingEnabled = true;
                    Debug.Log("[CS1Profiler] MethodProfiler initialized with Harmony");
                }
                else
                {
                    Debug.LogWarning("[CS1Profiler] Harmony is not installed, MethodProfiler disabled");
                }

                isInitialized = true;
                Debug.Log("[CS1Profiler] ProfilingManager initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] ProfilingManager.Initialize error: " + e.Message);
            }
        }

        // UI/設定からの制御メソッド
        public void SetProfilingEnabled(bool enabled)
        {
            isProfilingEnabled = enabled;
            CS1Profiler.Profiling.MethodProfiler.SetEnabled(enabled);
        }

        public void SetCSVExportInterval(int seconds)
        {
            csvExportInterval = seconds;
        }

        public void ToggleProfiling()
        {
            SetProfilingEnabled(!isProfilingEnabled);
        }

        public void LogCurrentStats()
        {
            if (csvManager != null)
            {
                csvManager.LogCurrentStats();
            }
        }

        public void PrintDetailedStats()
        {
            CS1Profiler.Profiling.MethodProfiler.PrintDetailedStats();
        }

        public void ExportToCSV()
        {
            if (csvManager != null)
            {
                csvManager.ExportToCSV();
            }
        }

        public string GetCsvPath()
        {
            return csvManager?.GetCsvPath() ?? "";
        }

        public bool IsProfilingEnabled()
        {
            return isProfilingEnabled;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                
                if (harmonyInstance != null)
                {
                    harmonyInstance.UnpatchAll("CS1Profiler.MethodProfiler");
                }
            }
        }
    }
}
